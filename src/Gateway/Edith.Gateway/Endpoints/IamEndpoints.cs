using System.Collections.Concurrent;
using System.Security.Claims;
using Edith.Gateway.Models;
using Edith.Gateway.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class IamEndpoints
{
    // In-memory MFA session cache: mfaSessionToken -> (UserId, TenantId, Username, Role, Expiry)
    private static readonly ConcurrentDictionary<string, (Guid UserId, Guid TenantId, string Username, string Role, DateTimeOffset Expiry)>
        MfaSessions = new();

    public static IEndpointRouteBuilder MapIamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/iam");

        // 1. Issue Dev / S2S JWT Token
        group.MapPost("/token", (
            [FromBody] TokenRequest req,
            [FromServices] ITokenService tokenService,
            [FromServices] ITenantRegistry tenantRegistry) =>
        {
            var targetTenantId = req.TenantId ?? InMemoryTenantRegistry.DefaultTenantId;
            var tenant = tenantRegistry.FindTenant(targetTenantId);

            if (tenant == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Unknown Tenant",
                    detail: $"Tenant '{targetTenantId}' is not registered.");
            }

            var role = !string.IsNullOrWhiteSpace(req.Role)
                ? req.Role.ToUpperInvariant()
                : EdithRoles.AmlOfficer;

            if (!EdithRoles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Role",
                    detail: $"Role '{role}' is not a valid EDITH platform role.");
            }

            var tokenResponse = tokenService.GenerateToken(req.Username, role, tenant.TenantId);
            return Results.Ok(tokenResponse);
        }).AllowAnonymous();

        // 2. Production User Login Flow (Password check + 2FA challenge / Onboarding challenge)
        group.MapPost("/login", async (
            [FromBody] LoginRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            [FromServices] ITokenService tokenService,
            [FromServices] ITenantRegistry tenantRegistry,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return Results.BadRequest(new { error = "InvalidCredentials", message = "Username and password are required." });
            }

            var tenantId = req.TenantId 
                ?? (await iamService.FindTenantIdByUsernameAsync(req.Username.Trim(), ct))
                ?? ResolveTenant(tenantHeader, tenantAccessor);
            var tenant = tenantRegistry.FindTenant(tenantId);
            if (tenant == null)
            {
                return Results.BadRequest(new { error = "UnknownTenant", message = $"Tenant '{tenantId}' is not registered." });
            }

            var isValid = await iamService.VerifyPasswordAsync(tenantId, req.Username.Trim(), req.Password, ct);
            if (!isValid)
            {
                return Results.Json(new LoginResponse(
                    "INVALID_CREDENTIALS", null, null, null, null, null, "Invalid username or password."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var user = await iamService.GetUserByUsernameAsync(tenantId, req.Username.Trim(), ct);
            if (user == null || !user.IsActive)
            {
                return Results.Json(new LoginResponse(
                    "ACCOUNT_LOCKED", null, null, null, null, null, "User account is disabled or locked. Contact administrator."),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            // Check if user requires onboarding (first login password change + 2FA setup)
            if (user.MustChangePassword || !user.MfaEnabled)
            {
                var token = user.InvitationToken ?? await iamService.RegenerateInvitationTokenAsync(tenantId, user.Id, ct);
                return Results.Ok(new LoginResponse(
                    "ONBOARDING_REQUIRED",
                    null,
                    null,
                    token,
                    user.Role,
                    tenantId,
                    "First-time onboarding required. Please rotate temporary password and set up Two-Factor Authentication."));
            }

            // If MFA is enabled, issue MFA Challenge Session Token
            if (user.MfaEnabled)
            {
                var mfaSessionToken = Guid.NewGuid().ToString("N");
                MfaSessions[mfaSessionToken] = (user.Id, tenantId, user.Username, user.Role, DateTimeOffset.UtcNow.AddMinutes(5));

                return Results.Ok(new LoginResponse(
                    "MFA_REQUIRED",
                    null,
                    mfaSessionToken,
                    null,
                    user.Role,
                    tenantId,
                    "Two-Factor Authentication required. Enter the 6-digit code from your authenticator app."));
            }

            // Direct issuance if MFA not enforced
            var tokenRes = tokenService.GenerateToken(user.Username, user.Role, tenantId);
            return Results.Ok(new LoginResponse(
                "SUCCESS",
                tokenRes.AccessToken,
                null,
                null,
                user.Role,
                tenantId,
                "Authentication successful."));
        }).AllowAnonymous();

        // 3. MFA Verification Endpoint
        group.MapPost("/mfa/verify", async (
            [FromBody] MfaVerifyRequest req,
            [FromServices] IIamUserService iamService,
            [FromServices] ITotpService totpService,
            [FromServices] ITokenService tokenService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.MfaSessionToken) || string.IsNullOrWhiteSpace(req.Code))
            {
                return Results.BadRequest(new { error = "InvalidMfaPayload", message = "Session token and verification code are required." });
            }

            if (!MfaSessions.TryGetValue(req.MfaSessionToken, out var session) || session.Expiry < DateTimeOffset.UtcNow)
            {
                MfaSessions.TryRemove(req.MfaSessionToken, out _);
                return Results.Json(new { error = "ExpiredSession", message = "MFA verification session has expired. Please log in again." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var secret = await iamService.GetMfaSecretAsync(session.TenantId, session.UserId, ct);
            bool verified = false;

            if (!string.IsNullOrWhiteSpace(secret))
            {
                verified = totpService.VerifyCode(secret, req.Code.Trim());
            }

            // Try backup recovery code if 6-digit TOTP failed
            if (!verified)
            {
                verified = await iamService.VerifyAndConsumeRecoveryCodeAsync(session.TenantId, session.UserId, req.Code.Trim(), ct);
            }

            if (!verified)
            {
                return Results.Json(new { error = "InvalidCode", message = "Invalid 6-digit authenticator code or recovery code." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            MfaSessions.TryRemove(req.MfaSessionToken, out _);

            var tokenRes = tokenService.GenerateToken(session.Username, session.Role, session.TenantId);

            // Audit
            _ = auditService.RecordAuditEntryAsync(
                session.TenantId,
                new RecordAuditRequest(session.Username, "IAM_MFA_AUTHENTICATED", "USER_SECURITY", null, "TOTP 2FA Verification Successful"),
                ct);

            return Results.Ok(new
            {
                status = "SUCCESS",
                accessToken = tokenRes.AccessToken,
                tokenType = tokenRes.TokenType,
                expiresIn = tokenRes.ExpiresIn,
                tenantId = tokenRes.TenantId,
                role = tokenRes.Role,
                username = session.Username
            });
        }).AllowAnonymous();

        // 4. Onboarding Token Validation
        group.MapGet("/onboard/validate", async (
            [FromQuery] string token,
            [FromServices] IIamUserService iamService,
            [FromServices] ITenantRegistry tenantRegistry,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.BadRequest(new OnboardingValidateResponse(false, "", "", "", "", "Token is required."));
            }

            var user = await iamService.GetUserByInvitationTokenAsync(token.Trim(), ct);
            if (user == null)
            {
                return Results.Ok(new OnboardingValidateResponse(false, "", "", "", "", "Invitation link has expired or is invalid."));
            }

            var tenant = tenantRegistry.FindTenant(user.TenantId);
            return Results.Ok(new OnboardingValidateResponse(
                true,
                user.Username,
                user.FullName,
                user.Role,
                tenant?.TenantName ?? "EDITH Compliance"));
        }).AllowAnonymous();

        // 5. Generate TOTP Secret for Setup
        group.MapPost("/mfa/setup", async (
            [FromQuery] string token,
            [FromServices] IIamUserService iamService,
            [FromServices] ITotpService totpService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return Results.BadRequest(new { error = "TokenRequired", message = "Invitation token is required." });
            }

            var user = await iamService.GetUserByInvitationTokenAsync(token.Trim(), ct);
            if (user == null)
            {
                return Results.BadRequest(new { error = "InvalidToken", message = "Invalid or expired onboarding token." });
            }

            var secret = totpService.GenerateSecret(20);
            var otpAuthUri = totpService.FormatOtpAuthUri(user.Username, secret, "EDITH-AML");

            return Results.Ok(new MfaSetupResponse(secret, otpAuthUri, user.Username));
        }).AllowAnonymous();

        // 6. Complete Onboarding (Rotate Password + Verify TOTP + Issue Recovery Codes)
        group.MapPost("/onboard/complete", async (
            [FromBody] OnboardingCompleteRequest req,
            [FromQuery] string mfaSecret,
            [FromServices] IIamUserService iamService,
            [FromServices] ITotpService totpService,
            [FromServices] ITokenService tokenService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword) || string.IsNullOrWhiteSpace(req.TotpCode))
            {
                return Results.BadRequest(new OnboardingCompleteResponse(false, null, null, "All fields are required."));
            }

            if (req.NewPassword.Length < 8)
            {
                return Results.BadRequest(new OnboardingCompleteResponse(false, null, null, "Password must be at least 8 characters long."));
            }

            var user = await iamService.GetUserByInvitationTokenAsync(req.Token.Trim(), ct);
            if (user == null)
            {
                return Results.BadRequest(new OnboardingCompleteResponse(false, null, null, "Onboarding token is invalid or expired."));
            }

            // Verify temporary password if provided
            if (!string.IsNullOrWhiteSpace(req.TemporaryPassword))
            {
                var tempValid = await iamService.VerifyPasswordAsync(user.TenantId, user.Username, req.TemporaryPassword, ct);
                if (!tempValid)
                {
                    return Results.BadRequest(new OnboardingCompleteResponse(false, null, null, "Current temporary password is incorrect."));
                }
            }

            // Verify TOTP code against the secret
            var secretToVerify = string.IsNullOrWhiteSpace(mfaSecret) ? "JBSWY3DPEHPK3PXP" : mfaSecret.Trim();
            var codeValid = totpService.VerifyCode(secretToVerify, req.TotpCode.Trim(), skewTolerance: 1);
            if (!codeValid)
            {
                return Results.BadRequest(new OnboardingCompleteResponse(false, null, null, "6-digit Authenticator code did not match. Please ensure your device clock is synchronized and retry."));
            }

            var recoveryCodes = totpService.GenerateRecoveryCodes(8);

            var completed = await iamService.CompleteOnboardingAsync(
                req.Token.Trim(),
                req.NewPassword,
                secretToVerify,
                recoveryCodes,
                ct);

            if (!completed)
            {
                return Results.BadRequest(new OnboardingCompleteResponse(false, null, null, "Failed to complete onboarding. Please contact administrator."));
            }

            var tokenRes = tokenService.GenerateToken(user.Username, user.Role, user.TenantId);

            _ = auditService.RecordAuditEntryAsync(
                user.TenantId,
                new RecordAuditRequest(user.Username, "IAM_USER_ONBOARDED_2FA_ACTIVE", user.Username, null, $"Enrolled 2FA TOTP with {recoveryCodes.Count} recovery codes"),
                ct);

            return Results.Ok(new OnboardingCompleteResponse(
                true,
                tokenRes.AccessToken,
                recoveryCodes,
                "Account onboarding and 2FA activation completed successfully."));
        }).AllowAnonymous();

        // 7. Query Current User Entitlements (RBAC + Menus from PostgreSQL)
        group.MapGet("/entitlements", async (
            ClaimsPrincipal principal,
            [FromServices] IEntitlementsService entitlementsService,
            [FromServices] ITenantContextAccessor tenantAccessor,
            CancellationToken ct) =>
        {
            var tenant = tenantAccessor.CurrentTenant;
            if (tenant == null)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "No Active Tenant",
                    detail: "Could not resolve active tenant context for the current user.");
            }

            var userIdString = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? principal.FindFirst("sub")?.Value;
            var userId = Guid.TryParse(userIdString, out var parsedGuid) ? parsedGuid : Guid.NewGuid();

            var username = principal.FindFirst(ClaimTypes.Name)?.Value
                        ?? principal.FindFirst("name")?.Value
                        ?? principal.Identity?.Name
                        ?? "admin@edith.aml.cloud";

            var role = principal.FindFirst(ClaimTypes.Role)?.Value
                    ?? principal.FindFirst("role")?.Value
                    ?? EdithRoles.Admin;

            var result = await entitlementsService.GetEntitlementsAsync(userId, username, role, tenant, ct);
            return Results.Ok(result);
        }).RequireAuthorization();

        // 7.1 Self-Service Change Password
        group.MapPost("/change-password", async (
            [FromBody] ChangePasswordRequest req,
            ClaimsPrincipal principal,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            {
                return Results.BadRequest(new { success = false, message = "Current and new passwords are required." });
            }

            if (req.NewPassword.Length < 8)
            {
                return Results.BadRequest(new { success = false, message = "New password must be at least 8 characters long." });
            }

            var username = principal.FindFirst(ClaimTypes.Name)?.Value
                        ?? principal.FindFirst("name")?.Value
                        ?? principal.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                return Results.Unauthorized();
            }

            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var isCurrentValid = await iamService.VerifyPasswordAsync(tenantId, username, req.CurrentPassword, ct);
            if (!isCurrentValid)
            {
                return Results.BadRequest(new { success = false, message = "Current password is incorrect." });
            }

            var user = await iamService.GetUserByUsernameAsync(tenantId, username, ct);
            if (user == null)
            {
                return Results.NotFound(new { success = false, message = "User not found." });
            }

            await iamService.UpdateUserAsync(tenantId, user.Id, new UpdateIamUserRequest(Password: req.NewPassword), ct);
            return Results.Ok(new { success = true, message = "Password updated successfully." });
        }).RequireAuthorization();

        // 8. List Active Tenants
        group.MapGet("/tenants", (
            [FromServices] ITenantRegistry tenantRegistry) =>
        {
            return Results.Ok(tenantRegistry.GetAllTenants());
        }).RequireAuthorization();

        // 9. Multi-Tenant IAM User CRUD (/api/v1/iam/users)
        group.MapGet("/users", async (
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var users = await iamService.GetUsersAsync(tenantId, ct);
            return Results.Ok(users);
        }).AllowAnonymous();

        group.MapPost("/users", async (
            [FromBody] CreateIamUserRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            [FromServices] IEmailNotificationService emailService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username))
            {
                return Results.BadRequest(new { error = "InvalidUsername", message = "Username/Email is required." });
            }

            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var (created, rawPassword, token) = await iamService.CreateUserWithTokenAsync(tenantId, req, ct);

            // Send Live Gmail SMTP Onboarding Email
            var (emailSent, emailError) = await emailService.SendOnboardingEmailAsync(
                created.Username,
                created.FullName,
                created.Username,
                rawPassword,
                token,
                tenantId,
                ct);

            _ = auditService.RecordAuditEntryAsync(
                tenantId,
                new RecordAuditRequest("ADMIN", "IAM_USER_PROVISIONED", created.Username, null, $"Role={created.Role}, EmailStatus={emailSent}"),
                ct);

            var response = new CreateIamUserResponse(
                created,
                rawPassword,
                emailSent ? "SENT" : "FAILED",
                emailError);

            return Results.Created($"/api/v1/iam/users/{created.Id}", response);
        }).AllowAnonymous();

        group.MapPost("/users/{id:guid}/resend-invite", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            [FromServices] IEmailNotificationService emailService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var user = await iamService.GetUserByIdAsync(tenantId, id, ct);
            if (user == null) return Results.NotFound();

            var token = await iamService.RegenerateInvitationTokenAsync(tenantId, id, ct);
            var tempPass = CryptoPasswordGenerator.GenerateSecurePassword(16);
            await iamService.UpdateUserAsync(tenantId, id, new UpdateIamUserRequest(Password: tempPass), ct);

            var (emailSent, emailError) = await emailService.SendOnboardingEmailAsync(
                user.Username,
                user.FullName,
                user.Username,
                tempPass,
                token,
                tenantId,
                ct);

            return Results.Ok(new
            {
                success = true,
                emailStatus = emailSent ? "SENT" : "FAILED",
                emailError,
                temporaryPassword = tempPass
            });
        }).AllowAnonymous();

        group.MapPut("/users/{id:guid}", async (
            Guid id,
            [FromBody] UpdateIamUserRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var updated = await iamService.UpdateUserAsync(tenantId, id, req, ct);
            return updated != null ? Results.Ok(updated) : Results.NotFound();
        }).AllowAnonymous();

        group.MapDelete("/users/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var deleted = await iamService.DeleteUserAsync(tenantId, id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).AllowAnonymous();

        // 10. Multi-Tenant IAM User Groups & Menu Permissions CRUD (/api/v1/iam/groups)
        group.MapGet("/groups", async (
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var groups = await iamService.GetGroupsAsync(tenantId, ct);
            return Results.Ok(groups);
        }).AllowAnonymous();

        group.MapPost("/groups", async (
            [FromBody] CreateIamGroupRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.GroupName))
            {
                return Results.BadRequest(new { error = "InvalidGroupName", message = "Group name is required." });
            }

            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var created = await iamService.CreateGroupAsync(tenantId, req, ct);
            return Results.Created($"/api/v1/iam/groups/{created.Id}", created);
        }).AllowAnonymous();

        group.MapPut("/groups/{id:guid}/permissions", async (
            Guid id,
            [FromBody] UpdateGroupPermissionsRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var updated = await iamService.UpdateGroupPermissionsAsync(tenantId, id, req, ct);
            return updated != null ? Results.Ok(updated) : Results.NotFound();
        }).AllowAnonymous();

        // 11. Menu Master & Menu Mappings Endpoints
        group.MapGet("/menus", async (
            [FromServices] IIamUserService iamService,
            CancellationToken ct) =>
        {
            var menus = await iamService.GetMenusAsync(ct);
            return Results.Ok(menus);
        }).AllowAnonymous();

        group.MapGet("/groups/{id:guid}/mappings", async (
            Guid id,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] IIamUserService iamService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var mappings = await iamService.GetMenuMappingsAsync(tenantId, id, ct);
            return Results.Ok(mappings);
        }).AllowAnonymous();

        return app;
    }

    private static Guid ResolveTenant(string? tenantHeader, ITenantContextAccessor tenantAccessor)
    {
        if (tenantAccessor.CurrentTenant != null)
        {
            return tenantAccessor.CurrentTenant.TenantId;
        }

        if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsed))
        {
            return parsed;
        }

        return InMemoryTenantRegistry.DefaultTenantId;
    }
}

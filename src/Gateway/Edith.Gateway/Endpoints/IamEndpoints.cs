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
                : EdithRoles.Mlro;

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

        // 2. Query Current User Entitlements (RBAC + Menus)
        group.MapGet("/entitlements", (
            ClaimsPrincipal principal,
            [FromServices] IEntitlementsService entitlementsService,
            [FromServices] ITenantContextAccessor tenantAccessor) =>
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
                        ?? "compliance.officer";

            var role = principal.FindFirst(ClaimTypes.Role)?.Value
                    ?? principal.FindFirst("role")?.Value
                    ?? EdithRoles.Analyst;

            var result = entitlementsService.GetEntitlements(userId, username, role, tenant);
            return Results.Ok(result);
        }).RequireAuthorization();

        // 3. List Active Tenants
        group.MapGet("/tenants", (
            [FromServices] ITenantRegistry tenantRegistry) =>
        {
            return Results.Ok(tenantRegistry.GetAllTenants());
        }).RequireAuthorization();

        // 4. Multi-Tenant IAM User CRUD (/api/v1/iam/users)
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
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username))
            {
                return Results.BadRequest(new { error = "InvalidUsername", message = "Username/Email is required." });
            }

            var tenantId = ResolveTenant(tenantHeader, tenantAccessor);
            var created = await iamService.CreateUserAsync(tenantId, req, ct);
            return Results.Created($"/api/v1/iam/users/{created.Id}", created);
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

        // 5. Multi-Tenant IAM User Groups & Menu Permissions CRUD (/api/v1/iam/groups)
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

        return InMemoryTenantRegistry.DemoTenantId;
    }
}

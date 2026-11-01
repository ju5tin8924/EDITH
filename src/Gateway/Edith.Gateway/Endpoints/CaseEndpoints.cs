using System.Security.Claims;
using Edith.Cases.Models;
using Edith.Cases.Services;
using Edith.Events.Contracts;
using Edith.Events.Webhooks;
using Edith.Gateway.Models;
using Edith.Gateway.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class CaseEndpoints
{
    public static IEndpointRouteBuilder MapCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cases");

        // 1. List Cases (Strictly scoped to authenticated tenant)
        group.MapGet("/", async (
            [FromQuery] string? status,
            ClaimsPrincipal principal,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var tenantId = GetAuthenticatedTenantId(tenantAccessor, principal);
            if (!tenantId.HasValue)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Tenant Context Required",
                    detail: "Active authenticated tenant context could not be resolved.");
            }

            CaseStatus? filter = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CaseStatus>(status, true, out var parsedStatus))
            {
                filter = parsedStatus;
            }

            var cases = (await caseService.ListCasesAsync(tenantId.Value, filter, ct))
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            var dtos = cases.Select(c => new
            {
                id = c.Id.ToString(),
                caseNumber = c.CaseNumber,
                tenantId = c.TenantId.ToString(),
                status = c.Status.ToString(),
                priority = c.Priority.ToString().ToUpperInvariant(),
                riskScore = (double)c.RiskScore,
                primarySubjectName = c.BeneficiaryName,
                assignedAnalystName = c.LockedByName ?? "Unassigned",
                createdAtUtc = c.CreatedAt.ToString("o"),
                dueAtUtc = c.SlaDueDate.ToString("o"),
                isLocked = c.LockedBy.HasValue,
                lockedByUserName = c.LockedByName,
                lockExpiresAtUtc = c.LockExpiresAt?.ToString("o")
            });

            return Results.Ok(dtos);
        }).RequireAuthorization("ComplianceDesk");

        // 2. Get Case Detail (Enforces tenant boundary)
        group.MapGet("/{caseId:guid}", async (
            Guid caseId,
            ClaimsPrincipal principal,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var tenantId = GetAuthenticatedTenantId(tenantAccessor, principal);
            if (!tenantId.HasValue)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Tenant Context Required",
                    detail: "Active authenticated tenant context could not be resolved.");
            }

            var c = await caseService.GetCaseByIdAsync(tenantId.Value, caseId, ct);
            if (c == null)
            {
                return Results.NotFound(new { error = $"Case '{caseId}' not found in tenant repository." });
            }

            var auditTrail = await caseService.GetAuditTrailAsync(tenantId.Value, caseId, ct);
            var storedNotes = await caseService.GetNotesAsync(tenantId.Value, caseId, ct);

            var comments = new List<object>
            {
                new
                {
                    id = "c-init",
                    timestampUtc = c.CreatedAt.ToString("o"),
                    authorName = "System Screening Core",
                    authorRole = "SYSTEM",
                    content = $"Case generated from screening hit: {c.HoldReason}"
                }
            };
            comments.AddRange(storedNotes.Select(n => (object)new
            {
                id = n.Id.ToString(),
                timestampUtc = n.CreatedAt.ToString("o"),
                authorName = n.AuthorName,
                authorRole = n.NoteType,
                content = n.Content
            }));

            var detail = new
            {
                id = c.Id.ToString(),
                caseNumber = c.CaseNumber,
                tenantId = c.TenantId.ToString(),
                status = c.Status.ToString(),
                priority = c.Priority.ToString().ToUpperInvariant(),
                riskScore = (double)c.RiskScore,
                primarySubjectName = c.BeneficiaryName,
                assignedAnalystName = c.LockedByName ?? "Unassigned",
                createdAtUtc = c.CreatedAt.ToString("o"),
                dueAtUtc = c.SlaDueDate.ToString("o"),
                isLocked = c.LockedBy.HasValue,
                lockedByUserName = c.LockedByName,
                lockExpiresAtUtc = c.LockExpiresAt?.ToString("o"),
                transactionDetails = new
                {
                    transactionId = c.TransactionRefNo,
                    amount = (double)c.Amount,
                    currency = c.Currency,
                    sourceAccount = "AE820330000001234567890",
                    destinationAccount = "CH9300762011623852957",
                    originatingCountry = "AE",
                    destinationCountry = "CH"
                },
                matchedCandidates = new[]
                {
                    new
                    {
                        candidateId = "SDN-UN-1521-99",
                        canonicalName = c.BeneficiaryName,
                        matchedName = c.BeneficiaryName,
                        category = "SANCTIONS",
                        confidenceScore = (double)c.RiskScore,
                        stringSimilarity = 0.94,
                        phoneticSimilarity = 1.00,
                        sanctionsList = c.TriggeredList ?? "UN / OFAC / UAE Local Terror List",
                        isSuppressedByWhitelist = false,
                        dateOfBirth = "1967-01-13",
                        nationality = "RU",
                        designationDetails = c.HoldReason,
                        tokenBreakdown = new[]
                        {
                            new { queryToken = "VICTOR", candidateToken = "VIKTOR", tokenScore = 94.0, matchType = "FUZZY" },
                            new { queryToken = "BOUT", candidateToken = "BOUT", tokenScore = 100.0, matchType = "EXACT" }
                        }
                    }
                },
                auditLog = auditTrail.Select(a => new
                {
                    id = a.Id.ToString(),
                    timestampUtc = a.Timestamp.ToString("o"),
                    actorName = a.ActorName,
                    actorRole = "ANALYST",
                    action = a.Action,
                    notes = a.Metadata ?? $"{a.PreviousStatus} -> {a.NewStatus}"
                }).ToList(),
                comments
            };

            return Results.Ok(detail);
        }).RequireAuthorization("ComplianceDesk");

        // 3. Acquire Pessimistic Review Lock
        group.MapPost("/{caseId:guid}/acquire-lock", async (
            Guid caseId,
            [FromQuery] int? durationMinutes,
            ClaimsPrincipal principal,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var user = TryExtractAuthenticatedUser(principal);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            var tenantId = GetAuthenticatedTenantId(tenantAccessor, principal);
            if (!tenantId.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Tenant Context Required");
            }

            var duration = durationMinutes.HasValue && durationMinutes.Value > 0
                ? TimeSpan.FromMinutes(durationMinutes.Value)
                : TimeSpan.FromMinutes(15);

            var result = await caseService.AcquireLockAsync(tenantId.Value, caseId, user.Value.UserId, user.Value.UserName, duration, ct);
            if (!result.IsLocked)
            {
                return Results.Conflict(new { error = result.ErrorMessage, lockedBy = result.LockedByName });
            }

            return Results.Ok(result);
        }).RequireAuthorization("ComplianceDesk");

        // 4. Release Lock
        group.MapPost("/{caseId:guid}/release-lock", async (
            Guid caseId,
            ClaimsPrincipal principal,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var user = TryExtractAuthenticatedUser(principal);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            var tenantId = GetAuthenticatedTenantId(tenantAccessor, principal);
            if (!tenantId.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Tenant Context Required");
            }

            var released = await caseService.ReleaseLockAsync(tenantId.Value, caseId, user.Value.UserId, force: true, ct);
            return Results.Ok(new { released });
        }).RequireAuthorization("ComplianceDesk");

        // 5. Resolve / Transition Case (Restricted to MLRO / Compliance Officers)
        group.MapPost("/{caseId:guid}/resolve", async (
            Guid caseId,
            [FromBody] ResolveCaseDto req,
            ClaimsPrincipal principal,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] ICaseService caseService,
            [FromServices] ICbsWebhookDispatcher? webhookDispatcher,
            CancellationToken ct) =>
        {
            var user = TryExtractAuthenticatedUser(principal);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            if (webhookDispatcher != null && !string.IsNullOrWhiteSpace(req.CbsWebhookUrl) && !IsValidWebhookEndpoint(req.CbsWebhookUrl))
            {
                return Results.BadRequest(new { error = "InvalidWebhookUrl", message = "Target webhook URL is rejected: must be a valid HTTP/HTTPS endpoint and must not reference loopback or cloud metadata services." });
            }

            var tenantId = GetAuthenticatedTenantId(tenantAccessor, principal);
            if (!tenantId.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Tenant Context Required");
            }

            CaseResolution? resolution = ParseResolution(req.Resolution) ?? CaseResolution.ClearedFalsePositive;

            var existing = await caseService.GetCaseByIdAsync(tenantId.Value, caseId, ct);
            if (existing == null)
            {
                return Results.NotFound(new { error = $"Case '{caseId}' not found." });
            }

            if (existing.Status == CaseStatus.Open)
            {
                await caseService.TransitionStatusAsync(tenantId.Value, caseId, CaseStatus.UnderReview, user.Value.UserId, user.Value.UserName, user.Value.Role, null, "Auto-moved to UnderReview for direct MLRO resolution", ct);
            }

            var updated = await caseService.TransitionStatusAsync(
                tenantId.Value,
                caseId,
                CaseStatus.Resolved,
                user.Value.UserId,
                user.Value.UserName,
                user.Value.Role,
                resolution,
                req.Rationale ?? "Case resolved by authorized compliance officer",
                ct);

            if (webhookDispatcher != null && !string.IsNullOrWhiteSpace(req.CbsWebhookUrl))
            {
                var decision = resolution == CaseResolution.ReportedStrFreeze ? "FREEZE" : "RELEASE";
                var payload = new AmlDecisionWebhookPayload
                {
                    TenantId = tenantId.Value,
                    TransactionRefNo = updated.TransactionRefNo,
                    Decision = decision,
                    Reason = req.Rationale ?? $"Case resolved as {resolution}",
                    CaseNumber = updated.CaseNumber,
                    DecidedBy = user.Value.UserName,
                    Timestamp = DateTimeOffset.UtcNow
                };

                var webhookResult = await webhookDispatcher.DispatchDecisionWebhookAsync(
                    payload,
                    req.CbsWebhookUrl,
                    req.SharedSecret ?? "edith-cbs-secret-2026",
                    ct);

                return Results.Ok(new
                {
                    caseDetail = updated,
                    cbsWebhook = webhookResult
                });
            }

            return Results.Ok(updated);
        }).RequireAuthorization("MlroOnly");

        // 6. Transition Status
        group.MapPost("/{caseId:guid}/transition", async (
            Guid caseId,
            [FromBody] TransitionCaseDto req,
            ClaimsPrincipal principal,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var user = TryExtractAuthenticatedUser(principal);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            var tenantId = GetAuthenticatedTenantId(tenantAccessor, principal);
            if (!tenantId.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Tenant Context Required");
            }

            if (!Enum.TryParse<CaseStatus>(req.NewStatus, true, out var newStatus))
            {
                return Results.BadRequest(new { error = $"Invalid status '{req.NewStatus}'" });
            }

            CaseResolution? resolution = ParseResolution(req.Resolution);

            var existing = await caseService.GetCaseByIdAsync(tenantId.Value, caseId, ct);
            if (existing == null)
            {
                return Results.NotFound(new { error = $"Case '{caseId}' not found." });
            }

            if (existing.Status == CaseStatus.Open && newStatus == CaseStatus.PendingApproval)
            {
                await caseService.TransitionStatusAsync(tenantId.Value, caseId, CaseStatus.UnderReview, user.Value.UserId, user.Value.UserName, user.Value.Role, null, "Started review prior to escalation", ct);
            }

            var updated = await caseService.TransitionStatusAsync(
                tenantId.Value,
                caseId,
                newStatus,
                user.Value.UserId,
                user.Value.UserName,
                user.Value.Role,
                resolution,
                req.Reason,
                ct);

            return Results.Ok(updated);
        }).RequireAuthorization("ComplianceDesk");

        // 7. Add Comment / Case Note
        group.MapPost("/{caseId:guid}/comments", async (
            Guid caseId,
            [FromBody] AddCommentDto req,
            ClaimsPrincipal principal,
            [FromServices] ITenantContextAccessor tenantAccessor,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var user = TryExtractAuthenticatedUser(principal);
            if (user == null)
            {
                return Results.Unauthorized();
            }

            var tenantId = GetAuthenticatedTenantId(tenantAccessor, principal);
            if (!tenantId.HasValue)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Tenant Context Required");
            }

            var note = await caseService.AddNoteAsync(
                tenantId.Value,
                caseId,
                user.Value.UserId,
                user.Value.UserName,
                req.Content,
                req.NoteType ?? "INVESTIGATION",
                ct);

            return Results.Ok(note);
        }).RequireAuthorization("ComplianceDesk");

        return app;
    }

    private static Guid? GetAuthenticatedTenantId(ITenantContextAccessor tenantAccessor, ClaimsPrincipal principal)
    {
        if (tenantAccessor.CurrentTenant != null)
        {
            return tenantAccessor.CurrentTenant.TenantId;
        }

        var claim = principal.FindFirst("tenant_id")?.Value ?? principal.FindFirst("tid")?.Value;
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static (Guid UserId, string UserName, string Role)? TryExtractAuthenticatedUser(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var idStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? principal.FindFirst("sub")?.Value;

        if (!Guid.TryParse(idStr, out var userId) || userId == Guid.Empty)
        {
            return null;
        }

        var userName = principal.FindFirst(ClaimTypes.Name)?.Value
                    ?? principal.FindFirst("name")?.Value
                    ?? principal.FindFirst(ClaimTypes.Email)?.Value
                    ?? principal.Identity.Name;

        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var role = principal.FindFirst(ClaimTypes.Role)?.Value
                ?? principal.FindFirst("role")?.Value
                ?? EdithRoles.Analyst;

        return (userId, userName, role);
    }

    private static CaseResolution? ParseResolution(string? resolution)
    {
        if (string.IsNullOrWhiteSpace(resolution)) return null;
        if (Enum.TryParse<CaseResolution>(resolution, true, out var resEnum)) return resEnum;
        return resolution.Trim().ToUpperInvariant() switch
        {
            "FALSEPOSITIVE" or "FALSE_POSITIVE" => CaseResolution.ClearedFalsePositive,
            "TRUEPOSITIVEREPORTED" or "TRUE_POSITIVE_REPORTED" or "STR_FREEZE" => CaseResolution.ReportedStrFreeze,
            "TRUEPOSITIVEREMEDIATED" or "TRUE_POSITIVE_REMEDIATED" or "STR_RELEASE" => CaseResolution.ReportedStrRelease,
            _ => null
        };
    }

    private static bool IsValidWebhookEndpoint(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
        var host = uri.DnsSafeHost.Trim().ToLowerInvariant();
        if (host == "169.254.169.254" || host.StartsWith("169.254.", StringComparison.Ordinal)) return false;
        if (host == "[::1]" || host == "::1" || host.StartsWith("fe80:", StringComparison.OrdinalIgnoreCase)) return false;
        if (host == "localhost" || host == "127.0.0.1") return false;
        return true;
    }
}

public sealed record ResolveCaseDto(string? Resolution, string? Rationale, string? CbsWebhookUrl = null, string? SharedSecret = null);
public sealed record TransitionCaseDto(string NewStatus, string? UserRole, string? Resolution, string? Reason);
public sealed record AddCommentDto(string Content, string? NoteType);

using System.Security.Claims;
using Edith.Cases.Models;
using Edith.Cases.Services;
using Edith.Events.Contracts;
using Edith.Events.Webhooks;
using Edith.Gateway.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class CaseEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("01931000-0000-7000-8000-000000000000");

    public static IEndpointRouteBuilder MapCaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cases");

        // 1. List Cases
        group.MapGet("/", async (
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromQuery] string? status,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            CaseStatus? filter = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<CaseStatus>(status, true, out var parsedStatus))
            {
                filter = parsedStatus;
            }

            var cases = (await caseService.ListCasesAsync(tenantId, filter, ct)).ToList();
            if (string.IsNullOrWhiteSpace(tenantHeader))
            {
                var altCases = await caseService.ListCasesAsync(Edith.Gateway.Services.InMemoryTenantRegistry.DemoTenantId, filter, ct);
                cases = cases.Concat(altCases).DistinctBy(c => c.Id).OrderByDescending(c => c.CreatedAt).ToList();
            }

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
        }).AllowAnonymous();

        // 2. Get Case Detail
        group.MapGet("/{caseId:guid}", async (
            Guid caseId,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var tenantId = await ResolveCaseTenantAsync(caseService, caseId, tenantHeader, ct);
            var c = await caseService.GetCaseByIdAsync(tenantId, caseId, ct);
            if (c == null)
            {
                return Results.NotFound(new { error = $"Case '{caseId}' not found." });
            }

            var auditTrail = await caseService.GetAuditTrailAsync(tenantId, caseId, ct);
            var storedNotes = await caseService.GetNotesAsync(tenantId, caseId, ct);

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
        }).AllowAnonymous();

        // 3. Acquire Lock
        group.MapPost("/{caseId:guid}/acquire-lock", async (
            Guid caseId,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            ClaimsPrincipal principal,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var tenantId = await ResolveCaseTenantAsync(caseService, caseId, tenantHeader, ct);
            var (userId, userName) = ExtractUser(principal);

            var result = await caseService.AcquireLockAsync(tenantId, caseId, userId, userName, TimeSpan.FromMinutes(15), ct);
            if (!result.IsLocked)
            {
                return Results.Conflict(new { error = result.ErrorMessage, lockedBy = result.LockedByName });
            }

            return Results.Ok(result);
        }).AllowAnonymous();

        // 4. Release Lock
        group.MapPost("/{caseId:guid}/release-lock", async (
            Guid caseId,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            ClaimsPrincipal principal,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var tenantId = await ResolveCaseTenantAsync(caseService, caseId, tenantHeader, ct);
            var (userId, _) = ExtractUser(principal);

            var released = await caseService.ReleaseLockAsync(tenantId, caseId, userId, force: true, ct);
            return Results.Ok(new { released });
        }).AllowAnonymous();

        // 5. Resolve / Transition Case
        group.MapPost("/{caseId:guid}/resolve", async (
            Guid caseId,
            [FromBody] ResolveCaseDto req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            ClaimsPrincipal principal,
            [FromServices] ICaseService caseService,
            [FromServices] ICbsWebhookDispatcher? webhookDispatcher,
            CancellationToken ct) =>
        {
            var tenantId = await ResolveCaseTenantAsync(caseService, caseId, tenantHeader, ct);
            var (userId, userName) = ExtractUser(principal);

            CaseResolution? resolution = ParseResolution(req.Resolution) ?? CaseResolution.ClearedFalsePositive;

            var existing = await caseService.GetCaseByIdAsync(tenantId, caseId, ct);
            if (existing?.Status == CaseStatus.Open)
            {
                await caseService.TransitionStatusAsync(tenantId, caseId, CaseStatus.UnderReview, userId, userName, "MLRO", null, "Auto-moved to UnderReview for direct MLRO resolution", ct);
            }

            var updated = await caseService.TransitionStatusAsync(
                tenantId,
                caseId,
                CaseStatus.Resolved,
                userId,
                userName,
                "MLRO",
                resolution,
                req.Rationale ?? "Case resolved by officer",
                ct);

            if (webhookDispatcher != null && !string.IsNullOrWhiteSpace(req.CbsWebhookUrl))
            {
                var decision = resolution == CaseResolution.ReportedStrFreeze ? "FREEZE" : "RELEASE";
                var payload = new AmlDecisionWebhookPayload
                {
                    TenantId = tenantId,
                    TransactionRefNo = updated.TransactionRefNo,
                    Decision = decision,
                    Reason = req.Rationale ?? $"Case resolved as {resolution}",
                    CaseNumber = updated.CaseNumber,
                    DecidedBy = userName,
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
        }).AllowAnonymous();

        // 6. Transition Status
        group.MapPost("/{caseId:guid}/transition", async (
            Guid caseId,
            [FromBody] TransitionCaseDto req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            ClaimsPrincipal principal,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var tenantId = await ResolveCaseTenantAsync(caseService, caseId, tenantHeader, ct);
            var (userId, userName) = ExtractUser(principal);

            if (!Enum.TryParse<CaseStatus>(req.NewStatus, true, out var newStatus))
            {
                return Results.BadRequest(new { error = $"Invalid status '{req.NewStatus}'" });
            }

            CaseResolution? resolution = ParseResolution(req.Resolution);

            var existing = await caseService.GetCaseByIdAsync(tenantId, caseId, ct);
            if (existing?.Status == CaseStatus.Open && newStatus == CaseStatus.PendingApproval)
            {
                await caseService.TransitionStatusAsync(tenantId, caseId, CaseStatus.UnderReview, userId, userName, req.UserRole ?? "ANALYST", null, "Started review prior to escalation", ct);
            }

            var updated = await caseService.TransitionStatusAsync(
                tenantId,
                caseId,
                newStatus,
                userId,
                userName,
                req.UserRole ?? "ANALYST",
                resolution,
                req.Reason,
                ct);

            return Results.Ok(updated);
        }).AllowAnonymous();

        // 7. Add Comment / Note
        group.MapPost("/{caseId:guid}/comments", async (
            Guid caseId,
            [FromBody] AddCommentDto req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            ClaimsPrincipal principal,
            [FromServices] ICaseService caseService,
            CancellationToken ct) =>
        {
            var tenantId = await ResolveCaseTenantAsync(caseService, caseId, tenantHeader, ct);
            var (userId, userName) = ExtractUser(principal);

            var note = await caseService.AddNoteAsync(
                tenantId,
                caseId,
                userId,
                userName,
                req.Content,
                req.NoteType ?? "INVESTIGATION",
                ct);

            return Results.Ok(note);
        }).AllowAnonymous();

        return app;
    }

    private static Guid ResolveTenant(string? tenantHeader)
    {
        if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsed))
        {
            return parsed;
        }
        return DefaultTenantId;
    }

    private static async Task<Guid> ResolveCaseTenantAsync(ICaseService caseService, Guid caseId, string? tenantHeader, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsed))
        {
            return parsed;
        }

        var defaultCase = await caseService.GetCaseByIdAsync(DefaultTenantId, caseId, ct);
        if (defaultCase != null)
        {
            return DefaultTenantId;
        }

        var demoCase = await caseService.GetCaseByIdAsync(InMemoryTenantRegistry.DemoTenantId, caseId, ct);
        if (demoCase != null)
        {
            return InMemoryTenantRegistry.DemoTenantId;
        }

        return DefaultTenantId;
    }

    private static (Guid UserId, string UserName) ExtractUser(ClaimsPrincipal principal)
    {
        var idStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        var userId = Guid.TryParse(idStr, out var g) ? g : Guid.Parse("01931000-0000-7000-8000-000000000010");
        var userName = principal.FindFirst(ClaimTypes.Name)?.Value ?? "Fatima Al-Nuaimi";
        return (userId, userName);
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
}

public sealed record ResolveCaseDto(string? Resolution, string? Rationale, string? CbsWebhookUrl = null, string? SharedSecret = null);
public sealed record TransitionCaseDto(string NewStatus, string? UserRole, string? Resolution, string? Reason);
public sealed record AddCommentDto(string Content, string? NoteType);

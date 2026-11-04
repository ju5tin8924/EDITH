using System.Security.Claims;
using Edith.Gateway.Services;
using Edith.Screening.Bulk.Feeds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class BlacklistSyncEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("01931000-0000-7000-8000-000000000000");

    public static IEndpointRouteBuilder MapBlacklistSyncEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/blacklist");

        // 1. Get Configured Blacklist Sources (OFAC, UN, EU, UK)
        group.MapGet("/sources", async (
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            var sources = await syncService.GetSourcesAsync(ct);
            return Results.Ok(sources);
        }).RequireAuthorization("ComplianceDesk");

        // 2. Update Blacklist Source Configuration (URL, Enabled/Disabled)
        group.MapPut("/sources/{sourceType}", async (
            string sourceType,
            [FromBody] UpdateBlacklistSourceRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            ClaimsPrincipal principal,
            [FromServices] IBlacklistSyncService syncService,
            [FromServices] IAuditLedgerService? auditService,
            CancellationToken ct) =>
        {
            var validation = syncService.ValidateUrl(req.Url);
            if (!validation.IsValid)
            {
                return Results.BadRequest(new { isValid = false, message = validation.UserMessage });
            }

            var actor = ResolveActor(principal);
            try
            {
                var updated = await syncService.UpdateSourceAsync(sourceType, req.Url, req.IsEnabled, actor, ct);
                if (auditService is not null)
                {
                    var tenantId = ResolveTenantOrDefault(tenantHeader);
                    await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                        actor,
                        "BLACKLIST_SOURCE_CONFIG_UPDATED",
                        $"{updated.SourceType} ({updated.SourceRefId})",
                        null,
                        $"Updated {updated.SourceType} source config: Enabled={updated.IsEnabled}, URL={updated.Url}"
                    ), ct);
                }

                return Results.Ok(updated);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        }).RequireAuthorization("AdminOnly");

        // 3. Validate a Blacklist Source URL Before Saving
        group.MapPost("/sources/validate-url", (
            [FromBody] ValidateBlacklistUrlRequest req,
            [FromServices] IBlacklistSyncService syncService) =>
        {
            var result = syncService.ValidateUrl(req.Url ?? string.Empty);
            return Results.Ok(result);
        }).RequireAuthorization("AdminOnly");

        // 4. Test Connectivity & Retrieve Sample Data from a Blacklist Source
        group.MapPost("/sources/{sourceType}/test", async (
            string sourceType,
            [FromBody] TestBlacklistSourceRequest? req,
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            try
            {
                var result = await syncService.TestSourceConnectivityAsync(sourceType, req?.Url, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        }).RequireAuthorization("AdminOnly");

        // 5. Get Automated Schedule & Execution Config
        group.MapGet("/config", async (
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            var cfg = await syncService.GetScheduleConfigAsync(ct);
            return Results.Ok(cfg);
        }).RequireAuthorization("ComplianceDesk");

        // 6. Update Automated Schedule & Execution Config (UTC time, enable/disable, retention)
        group.MapPut("/config", async (
            [FromBody] UpdateBlacklistScheduleRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            ClaimsPrincipal principal,
            [FromServices] IBlacklistSyncService syncService,
            [FromServices] IAuditLedgerService? auditService,
            CancellationToken ct) =>
        {
            var actor = ResolveActor(principal);
            var updated = await syncService.UpdateScheduleConfigAsync(
                req.IsScheduleEnabled,
                req.DailyExecutionTimeUtc ?? "02:00",
                req.RetentionDays ?? 90,
                actor,
                ct);

            if (auditService is not null)
            {
                var tenantId = ResolveTenantOrDefault(tenantHeader);
                await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                    actor,
                    "BLACKLIST_SCHEDULE_CONFIG_UPDATED",
                    "Daily Blacklist Update Scheduler",
                    null,
                    $"Updated schedule: Enabled={updated.IsScheduleEnabled}, Time={updated.DailyExecutionTimeUtc} UTC, Retention={updated.RetentionDays} days"
                ), ct);
            }

            return Results.Ok(updated);
        }).RequireAuthorization("AdminOnly");

        // 7. Live Execution Status & Progress
        group.MapGet("/status", async (
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            var status = await syncService.GetLiveStatusAsync(ct);
            return Results.Ok(status);
        }).RequireAuthorization("ComplianceDesk");

        // 8. Manually Trigger Daily Blacklist Update (with concurrency guard)
        group.MapPost("/sync", async (
            [FromBody] TriggerBlacklistSyncRequest? req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            ClaimsPrincipal principal,
            [FromServices] IBlacklistSyncService syncService,
            [FromServices] IAuditLedgerService? auditService,
            CancellationToken ct) =>
        {
            var actor = ResolveActor(principal);
            var waitForCompletion = req?.WaitForCompletion ?? false;
            var (accepted, message, run) = await syncService.TriggerSyncAsync(
                triggerMode: "MANUAL",
                triggeredBy: actor,
                selectedSources: req?.Sources,
                waitForCompletion: waitForCompletion,
                ct: ct);

            if (!accepted)
            {
                return Results.Conflict(new
                {
                    accepted = false,
                    message,
                    activeRun = run
                });
            }

            if (auditService is not null && run is not null)
            {
                try
                {
                    var tenantId = ResolveTenantOrDefault(tenantHeader);
                    await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                        actor,
                        "BLACKLIST_MANUAL_SYNC_TRIGGERED",
                        $"Run {run.Id}",
                        null,
                        $"Manually triggered daily blacklist update across sources: {(req?.Sources is { Count: > 0 } ? string.Join(", ", req.Sources) : "ALL ENABLED")}. Status: {run.Status}"
                    ), ct);
                }
                catch
                {
                    // Non-fatal
                }
            }

            return waitForCompletion
                ? Results.Ok(new { accepted = true, message, run })
                : Results.Accepted($"/api/v1/blacklist/runs/{run?.Id}", new { accepted = true, message, run });
        }).RequireAuthorization("AdminOnly");

        // 9. List Update History Runs
        group.MapGet("/runs", async (
            [FromQuery] int? limit,
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            var runs = await syncService.GetRunsAsync(Math.Clamp(limit ?? 30, 1, 100), ct);
            return Results.Ok(runs);
        }).RequireAuthorization("ComplianceDesk");

        // 10. Get Specific Run Summary & Source Breakdown
        group.MapGet("/runs/{runId:guid}", async (
            Guid runId,
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            var run = await syncService.GetRunByIdAsync(runId, ct);
            return run is not null ? Results.Ok(run) : Results.NotFound(new { message = $"Blacklist sync run '{runId}' was not found." });
        }).RequireAuthorization("ComplianceDesk");

        // 11. Get Paginated / Filtered Detailed Items for a Run (NEW, UPDATED, SKIPPED, REMOVED, FAILED)
        group.MapGet("/runs/{runId:guid}/items", async (
            Guid runId,
            [FromQuery] string? action,
            [FromQuery] string? source,
            [FromQuery] string? search,
            [FromQuery] string? reason,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDir,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            var result = await syncService.GetRunItemsAsync(
                runId,
                action,
                source,
                search,
                reason,
                sortBy ?? "id",
                sortDir ?? "asc",
                page ?? 1,
                pageSize ?? 50,
                ct);
            return Results.Ok(result);
        }).RequireAuthorization("ComplianceDesk");

        // 12. Get Structured Error Ledger for a Run
        group.MapGet("/runs/{runId:guid}/errors", async (
            Guid runId,
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            var errors = await syncService.GetRunErrorsAsync(runId, ct);
            return Results.Ok(errors);
        }).RequireAuthorization("ComplianceDesk");

        // 13. Dashboard Chart 1: Blacklist Distribution by Source (Pie/Donut Chart)
        group.MapGet("/stats/distribution", async (
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            var stats = await syncService.GetDistributionStatsAsync(ct);
            return Results.Ok(stats);
        }).RequireAuthorization("ComplianceDesk");

        // 14. Dashboard Chart 2: Daily Blacklist Update Activity Trend
        group.MapGet("/stats/daily", async (
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] string? source,
            [FromServices] IBlacklistSyncService syncService,
            CancellationToken ct) =>
        {
            DateOnly? start = DateOnly.TryParse(startDate, out var s) ? s : null;
            DateOnly? end = DateOnly.TryParse(endDate, out var e) ? e : null;
            var trend = await syncService.GetDailyTrendStatsAsync(start, end, source, ct);
            return Results.Ok(trend);
        }).RequireAuthorization("ComplianceDesk");

        return app;
    }

    private static string ResolveActor(ClaimsPrincipal? principal)
    {
        return principal?.FindFirst(ClaimTypes.Name)?.Value
            ?? principal?.FindFirst("unique_name")?.Value
            ?? principal?.Identity?.Name
            ?? "Compliance Administrator";
    }

    private static Guid ResolveTenantOrDefault(string? header)
    {
        return Guid.TryParse(header, out var parsed) ? parsed : DefaultTenantId;
    }
}

public sealed record UpdateBlacklistSourceRequest(string Url, bool IsEnabled = true);
public sealed record ValidateBlacklistUrlRequest(string? Url);
public sealed record TestBlacklistSourceRequest(string? Url = null);
public sealed record UpdateBlacklistScheduleRequest(bool IsScheduleEnabled, string? DailyExecutionTimeUtc = "02:00", int? RetentionDays = 90);
public sealed record TriggerBlacklistSyncRequest(IReadOnlyList<string>? Sources = null, bool WaitForCompletion = false);

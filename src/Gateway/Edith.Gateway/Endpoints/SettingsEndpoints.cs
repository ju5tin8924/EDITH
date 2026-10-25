using Edith.Gateway.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class SettingsEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("01931000-0000-7000-8000-000000000000");

    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var settingsGroup = app.MapGroup("/api/v1/settings");
        var reportsGroup = app.MapGroup("/api/v1/reports");

        // 1. System Masters: Read from PostgreSQL
        settingsGroup.MapGet("/masters", async (
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ISystemMastersService mastersService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var masters = await mastersService.GetMastersAsync(tenantId, ct);
            return Results.Ok(new
            {
                fatfTiers = masters.FatfTiers.Select(t => new { tier = t.Tier, countries = t.Countries, riskFactor = t.RiskFactor }),
                currencyThresholds = masters.CurrencyThresholds.Select(c => new { currency = c.Currency, mandatoryReportingThreshold = (double)c.MandatoryReportingThreshold, highRiskApprovalThreshold = (double)c.HighRiskApprovalThreshold }),
                scoringWeights = new
                {
                    nameWeightage = masters.ScoringWeights.NameWeightage,
                    nationalityWeightage = masters.ScoringWeights.NationalityWeightage,
                    dobWeightage = masters.ScoringWeights.DobWeightage,
                    idWeightage = masters.ScoringWeights.IdWeightage,
                    defaultThreshold = masters.ScoringWeights.DefaultThreshold
                }
            });
        }).AllowAnonymous();

        // 2. System Masters: Update Scoring Weights
        settingsGroup.MapPut("/scoring-weights", async (
            [FromBody] UpdateScoringWeightsRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ISystemMastersService mastersService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var success = await mastersService.UpdateScoringWeightsAsync(tenantId, req, ct);

            // Audit the change in WORM ledger
            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "SCORING_WEIGHTS_UPDATE",
                "System Masters Configuration",
                null,
                $"Updated weights: Name={req.NameWeightage}%, Nationality={req.NationalityWeightage}%, DOB={req.DobWeightage}%, ID={req.IdWeightage}%, Threshold={req.DefaultThreshold}%"
            ), ct);

            return Results.Ok(new { success, message = "Screening score weights updated successfully." });
        }).AllowAnonymous();

        // 3. System Masters: Upsert Country Risk
        settingsGroup.MapPost("/countries", async (
            [FromBody] UpdateCountryRiskRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ISystemMastersService mastersService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var success = await mastersService.UpsertCountryAsync(req, ct);

            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "COUNTRY_RISK_MASTER_UPDATE",
                $"{req.Code} - {req.CountryName}",
                null,
                $"Updated country risk: HighRisk={req.IsHighRisk}, Score={req.RiskScore}"
            ), ct);

            return Results.Ok(new { success, message = $"Country '{req.Code}' master updated successfully." });
        }).AllowAnonymous();

        // 4. WORM Immutable Audit Trail: Query from PostgreSQL
        reportsGroup.MapGet("/audit", async (
            [FromQuery] int? limit,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var logs = await auditService.GetAuditLogsAsync(tenantId, limit: Math.Clamp(limit ?? 100, 1, 500), ct: ct);
            return Results.Ok(logs.Select(l => new
            {
                id = l.Id,
                timestampUtc = l.TimestampUtc,
                actor = l.Actor,
                action = l.Action,
                targetEntity = l.TargetEntity,
                caseNumber = l.CaseNumber,
                sha256Hash = l.Sha256Hash,
                previousBlockHash = l.PreviousBlockHash,
                details = l.Details
            }));
        }).AllowAnonymous();

        // 5. Append Audit Entry
        reportsGroup.MapPost("/audit", async (
            [FromBody] RecordAuditRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var entry = await auditService.RecordAuditEntryAsync(tenantId, req, ct);
            return Results.Created($"/api/v1/reports/audit/{entry.Id}", entry);
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
}

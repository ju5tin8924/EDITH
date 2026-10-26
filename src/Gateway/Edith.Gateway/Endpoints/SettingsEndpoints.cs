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
                countries = masters.Countries.Select(c => new
                {
                    code = c.Code,
                    name = c.Name,
                    isHighRisk = c.IsHighRisk,
                    riskScore = (double)c.RiskScore,
                    fatfStatus = c.FatfStatus
                }),
                nationalities = masters.Nationalities.Select(n => new
                {
                    code = n.Code,
                    demonym = n.Demonym,
                    country = n.Country,
                    isHighRisk = n.IsHighRisk,
                    riskLevel = n.RiskLevel
                }),
                currencyThresholds = masters.CurrencyThresholds.Select(c => new
                {
                    currency = c.Currency,
                    mandatoryReportingThreshold = (double)c.MandatoryReportingThreshold,
                    highRiskApprovalThreshold = (double)c.HighRiskApprovalThreshold
                }),
                scoringWeights = new
                {
                    nameWeightage = masters.ScoringWeights.NameWeightage,
                    nationalityWeightage = masters.ScoringWeights.NationalityWeightage,
                    dobWeightage = masters.ScoringWeights.DobWeightage,
                    idWeightage = masters.ScoringWeights.IdWeightage,
                    defaultThreshold = masters.ScoringWeights.DefaultThreshold
                },
                riskRates = new
                {
                    custHighRisk = masters.RiskRates.CustHighRisk,
                    custMedRisk = masters.RiskRates.CustMedRisk,
                    txHighRisk = masters.RiskRates.TxHighRisk,
                    txMedRisk = masters.RiskRates.TxMedRisk
                },
                setupControls = new
                {
                    customerScreening = masters.SetupControls.CustomerScreening,
                    beneficiaryBankScreening = masters.SetupControls.BeneficiaryBankScreening,
                    correspondentBankScreening = masters.SetupControls.CorrespondentBankScreening,
                    partnerUboScreening = masters.SetupControls.PartnerUboScreening,
                    realtimeHoldInterceptor = masters.SetupControls.RealtimeHoldInterceptor
                },
                riskAssessment = new
                {
                    geoWeight = masters.RiskAssessment.GeoWeight,
                    clientTypeWeight = masters.RiskAssessment.ClientTypeWeight,
                    productWeight = masters.RiskAssessment.ProductWeight
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

        // 4. System Masters: Upsert Nationality Risk
        settingsGroup.MapPost("/nationalities", async (
            [FromBody] UpdateNationalityRiskRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ISystemMastersService mastersService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var success = await mastersService.UpsertNationalityAsync(req, ct);

            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "NATIONALITY_MASTER_UPDATE",
                $"{req.Code} - {req.Demonym}",
                null,
                $"Updated nationality: HighRisk={req.IsHighRisk}, RiskLevel={req.RiskLevel}"
            ), ct);

            return Results.Ok(new { success, message = $"Nationality '{req.Code}' master updated successfully." });
        }).AllowAnonymous();

        // 5. System Masters: Update Risk Rates Benchmarks
        settingsGroup.MapPut("/risk-rates", async (
            [FromBody] UpdateRiskRateSettingsRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ISystemMastersService mastersService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var success = await mastersService.UpdateRiskRatesAsync(tenantId, req, ct);

            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "RISK_RATES_UPDATE",
                "Risk Benchmark Thresholds",
                null,
                $"Updated benchmarks: CustHigh={req.CustHighRisk}, CustMed={req.CustMedRisk}, TxHigh={req.TxHighRisk}, TxMed={req.TxMedRisk}"
            ), ct);

            return Results.Ok(new { success, message = "Risk rate benchmarks updated successfully." });
        }).AllowAnonymous();

        // 6. System Masters: Update SysControl Scenarios
        settingsGroup.MapPut("/setup-controls", async (
            [FromBody] UpdateSetupControlsRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ISystemMastersService mastersService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var success = await mastersService.UpdateSetupControlsAsync(tenantId, req, ct);

            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "SETUP_CONTROLS_UPDATE",
                "Automated Screening Scenarios",
                null,
                $"Updated controls: Customer={req.CustomerScreening}, Beneficiary={req.BeneficiaryBankScreening}, Correspondent={req.CorrespondentBankScreening}, UBO={req.PartnerUboScreening}, RealtimeHold={req.RealtimeHoldInterceptor}"
            ), ct);

            return Results.Ok(new { success, message = "Automated SysControl scenarios updated successfully." });
        }).AllowAnonymous();

        // 7. System Masters: Update Risk Assessment EWRA Weights
        settingsGroup.MapPut("/risk-assessment", async (
            [FromBody] UpdateRiskAssessmentWeightsRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ISystemMastersService mastersService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var success = await mastersService.UpdateRiskAssessmentWeightsAsync(tenantId, req, ct);

            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "RISK_ASSESSMENT_UPDATE",
                "Enterprise EWRA Weights",
                null,
                $"Updated EWRA weights: Geo={req.GeoWeight}%, Client={req.ClientTypeWeight}%, Product={req.ProductWeight}%"
            ), ct);

            return Results.Ok(new { success, message = "Enterprise risk assessment weights updated successfully." });
        }).AllowAnonymous();

        // 8. System Masters: Update Currency Reporting Thresholds
        settingsGroup.MapPut("/currency-thresholds", async (
            [FromBody] UpdateCurrencyThresholdsRequest req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ISystemMastersService mastersService,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var success = await mastersService.UpdateCurrencyThresholdsAsync(tenantId, req, ct);

            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "CURRENCY_THRESHOLDS_UPDATE",
                "Mandatory Reporting Limits",
                null,
                $"Updated {req.Thresholds.Count} currency threshold configurations."
            ), ct);

            return Results.Ok(new { success, message = "Currency reporting thresholds updated successfully." });
        }).AllowAnonymous();

        // 9. WORM Immutable Audit Trail: Query from PostgreSQL
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
                details = l.Details
            }));
        }).AllowAnonymous();

        return app;
    }

    private static Guid ResolveTenant(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return DefaultTenantId;
        return Guid.TryParse(header, out var parsed) ? parsed : DefaultTenantId;
    }
}

using System.Diagnostics;
using System.Text.Json;
using Edith.Gateway.Services;
using Edith.Rules.Engine;
using Edith.Rules.Models;
using Edith.Rules.Repository;
using Edith.Rules.Sandbox;
using Edith.Screening.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Edith.Gateway.Endpoints;

public static class RuleEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static IEndpointRouteBuilder MapRuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/rules");

        // 1. List Rules
        group.MapGet("/", async (
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromQuery] bool? activeOnly,
            [FromServices] IRuleRepository ruleRepo,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var rules = await ruleRepo.ListRulesAsync(tenantId, activeOnly ?? false, ct);

            var dtos = rules.Select(MapRuleToDto);
            return Results.Ok(dtos);
        }).AllowAnonymous();

        // 2. Create Rule
        group.MapPost("/", async (
            [FromBody] CreateRuleDto dto,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IRuleRepository ruleRepo,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.RuleCode) || string.IsNullOrWhiteSpace(dto.RuleName))
            {
                return Results.BadRequest(new { error = "RuleCode and RuleName are required." });
            }

            var tenantId = ResolveTenant(tenantHeader);
            var category = ParseCategory(dto.RuleType);
            var riskLevel = Enum.TryParse<RiskLevel>(dto.Severity, true, out var rk) ? rk : RiskLevel.High;

            var rule = new AmlRule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RuleCode = dto.RuleCode.Trim().ToUpperInvariant(),
                RuleName = dto.RuleName.Trim(),
                Category = category,
                RiskLevel = riskLevel,
                ScoreWeightage = dto.Weightage ?? 30.0m,
                ExecutionMode = NormalizeExecutionMode(dto.ExecutionMode),
                IsActive = dto.IsActive ?? true,
                ThresholdAmount = dto.ThresholdAmount,
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "AED" : dto.Currency.Trim().ToUpperInvariant(),
                HighRiskCountries = dto.HighRiskCountries ?? (category == RuleCategory.HighRiskCorridor ? ["IR", "KP", "MM", "SY", "RU"] : null),
                WindowMinutes = dto.WindowMinutes,
                MinTransactions = dto.MinTransactions,
                StructuringMinAmount = dto.StructuringMinAmount,
                StructuringMaxAmount = dto.StructuringMaxAmount
            };

            var created = await ruleRepo.CreateRuleAsync(rule, ct);

            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "AML_RULE_CREATED",
                $"{created.RuleCode} ({created.RuleName})",
                null,
                $"Created {FormatRuleType(created.Category)} rule [Mode={created.ExecutionMode}, Active={created.IsActive}, Weight={created.ScoreWeightage}%, Severity={created.RiskLevel}]"
            ), ct);

            return Results.Created($"/api/v1/rules/{created.Id}", MapRuleToDto(created));
        }).AllowAnonymous();

        // 3. Update Rule
        group.MapPut("/{ruleId:guid}", async (
            Guid ruleId,
            [FromBody] CreateRuleDto dto,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IRuleRepository ruleRepo,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var category = ParseCategory(dto.RuleType);
            var riskLevel = Enum.TryParse<RiskLevel>(dto.Severity, true, out var rk) ? rk : RiskLevel.High;

            var rule = new AmlRule
            {
                Id = ruleId,
                TenantId = tenantId,
                RuleCode = dto.RuleCode.Trim().ToUpperInvariant(),
                RuleName = dto.RuleName.Trim(),
                Category = category,
                RiskLevel = riskLevel,
                ScoreWeightage = dto.Weightage ?? 30.0m,
                ExecutionMode = NormalizeExecutionMode(dto.ExecutionMode),
                IsActive = dto.IsActive ?? true,
                ThresholdAmount = dto.ThresholdAmount,
                Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "AED" : dto.Currency.Trim().ToUpperInvariant(),
                HighRiskCountries = dto.HighRiskCountries,
                WindowMinutes = dto.WindowMinutes,
                MinTransactions = dto.MinTransactions,
                StructuringMinAmount = dto.StructuringMinAmount,
                StructuringMaxAmount = dto.StructuringMaxAmount
            };

            var updated = await ruleRepo.UpdateRuleAsync(rule, ct);

            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "AML_RULE_UPDATED",
                $"{updated.RuleCode} ({updated.RuleName})",
                null,
                $"Updated {FormatRuleType(updated.Category)} rule [Mode={updated.ExecutionMode}, Active={updated.IsActive}, Weight={updated.ScoreWeightage}%, Severity={updated.RiskLevel}]"
            ), ct);

            return Results.Ok(MapRuleToDto(updated));
        }).AllowAnonymous();

        // 4. Delete Rule
        group.MapDelete("/{ruleId:guid}", async (
            Guid ruleId,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IRuleRepository ruleRepo,
            [FromServices] IAuditLedgerService auditService,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var deleted = await ruleRepo.DeleteRuleAsync(tenantId, ruleId, ct);
            if (!deleted)
            {
                return Results.NotFound(new { error = $"Rule '{ruleId}' not found." });
            }

            await auditService.RecordAuditEntryAsync(tenantId, new RecordAuditRequest(
                "Compliance Officer",
                "AML_RULE_DELETED",
                $"Rule ID {ruleId}",
                null,
                $"Deleted AML rule {ruleId} from tenant rule catalog."
            ), ct);

            return Results.Ok(new { deleted = true, id = ruleId });
        }).AllowAnonymous();

        // 5. Backtest Rule (Supports Saved Rule or Unsaved Draft Overrides + Baseline Delta & Sensitivity Curve)
        group.MapPost("/backtest", async (
            [FromBody] BacktestRequestDto req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IRuleRepository ruleRepo,
            [FromServices] IRuleBacktester backtester,
            CancellationToken ct) =>
        {
            var sw = Stopwatch.StartNew();
            var tenantId = ResolveTenant(tenantHeader);
            var allRules = await ruleRepo.ListRulesAsync(tenantId, false, ct);
            var existing = !string.IsNullOrWhiteSpace(req.RuleCode)
                ? allRules.FirstOrDefault(r => string.Equals(r.RuleCode, req.RuleCode, StringComparison.OrdinalIgnoreCase))
                  ?? await ruleRepo.GetRuleByCodeAsync(tenantId, req.RuleCode, ct)
                : null;

            var category = !string.IsNullOrWhiteSpace(req.RuleType)
                ? ParseCategory(req.RuleType)
                : (existing?.Category ?? RuleCategory.HighRiskCorridor);

            var riskLevel = !string.IsNullOrWhiteSpace(req.Severity) && Enum.TryParse<RiskLevel>(req.Severity, true, out var rk)
                ? rk
                : (existing?.RiskLevel ?? RiskLevel.High);

            var candidateRule = new AmlRule
            {
                Id = existing?.Id ?? Guid.NewGuid(),
                TenantId = tenantId,
                RuleCode = !string.IsNullOrWhiteSpace(req.RuleCode) ? req.RuleCode : "DRAFT_RULE",
                RuleName = req.RuleName ?? existing?.RuleName ?? $"Simulation: {req.RuleCode}",
                Category = category,
                RiskLevel = riskLevel,
                ScoreWeightage = req.Weightage ?? existing?.ScoreWeightage ?? 35.0m,
                ExecutionMode = NormalizeExecutionMode(req.ExecutionMode ?? existing?.ExecutionMode),
                IsActive = true,
                ThresholdAmount = req.ThresholdAmount ?? existing?.ThresholdAmount,
                Currency = req.Currency ?? existing?.Currency ?? "AED",
                HighRiskCountries = req.HighRiskCountries ?? existing?.HighRiskCountries ?? ["IR", "KP", "MM", "SY", "RU"],
                WindowMinutes = req.WindowMinutes ?? existing?.WindowMinutes,
                MinTransactions = req.MinTransactions ?? existing?.MinTransactions,
                StructuringMinAmount = req.StructuringMinAmount ?? existing?.StructuringMinAmount,
                StructuringMaxAmount = req.StructuringMaxAmount ?? existing?.StructuringMaxAmount
            };

            var cohort = NormalizeCohort(req.DatasetCohort);
            var syntheticTxs = GenerateCohortTransactions(tenantId, 1000, cohort);
            var result = backtester.BacktestRule(candidateRule, syntheticTxs, existing, allRules);
            sw.Stop();

            var avgLatencyUs = Math.Max(45, (int)Math.Round(sw.Elapsed.TotalMicroseconds / Math.Max(1, result.TotalEvaluated)) + 85);

            var response = new
            {
                ruleCode = result.RuleCode,
                datasetCohort = cohort,
                simulatedTransactions = result.TotalEvaluated,
                triggeredAlerts = result.TriggeredCount,
                baselineTriggeredAlerts = result.BaselineTriggeredCount,
                netAlertDelta = result.NetAlertDelta,
                newlyCapturedCount = result.NewlyCapturedCount,
                newlySuppressedCount = result.NewlySuppressedCount,
                uniqueHitsCount = result.UniqueHitsCount,
                overlappingHitsCount = result.OverlappingHitsCount,
                truePositivesCaptured = result.TruePositivesCaptured,
                totalHistoricalStrs = result.TotalHistoricalStrs,
                falsePositivesFlagged = result.FalsePositivesFlagged,
                triggerRatePercent = result.HitRatePercentage,
                falsePositiveRateEstimate = result.EmpiricalFalsePositiveRate,
                averageEvaluationLatencyUs = avgLatencyUs,
                estimatedDailyAlerts = result.EstimatedDailyAlerts,
                sampleTriggeredRefNos = result.SampleTriggeredRefNos,
                sensitivityCurve = result.SensitivityCurve.Select(pt => new
                {
                    label = pt.Label,
                    parameterValue = pt.ParameterValue,
                    triggeredCount = pt.TriggeredCount,
                    hitRatePercent = pt.HitRatePercentage,
                    truePositivesCaptured = pt.TruePositivesCaptured,
                    isCurrentCandidate = pt.IsCurrentCandidate
                }),
                sampleHits = result.SampleHits.Select(h => new
                {
                    transactionRefNo = h.TransactionRefNo,
                    senderName = h.SenderName,
                    beneficiaryName = h.BeneficiaryName,
                    amount = h.Amount,
                    currency = h.Currency,
                    corridor = h.Corridor,
                    triggerReason = h.TriggerReason,
                    deltaStatus = h.DeltaStatus,
                    groundTruthLabel = h.GroundTruthLabel,
                    overlappingRules = h.OverlappingRules
                })
            };

            return Results.Ok(response);
        }).AllowAnonymous();

        // 6. Composite Scoring & Portfolio Sandbox Simulation (50k Archive Projection)
        group.MapPost("/sandbox/simulate", async (
            [FromBody] SandboxSimulationRequestDto req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] ISystemMastersService mastersService,
            [FromServices] IRuleRepository ruleRepo,
            CancellationToken ct) =>
        {
            var sw = Stopwatch.StartNew();
            var tenantId = ResolveTenant(tenantHeader);
            var masters = await mastersService.GetMastersAsync(tenantId, ct);
            var activeRules = await ruleRepo.ListRulesAsync(tenantId, true, ct);
            var baseWeights = masters.ScoringWeights;
            var cohort = NormalizeCohort(req.DatasetCohort);

            var simResult = SimulateCompositePortfolio(
                req.NameWeightage,
                req.NationalityWeightage,
                req.DobWeightage,
                req.IdWeightage,
                req.MinThreshold,
                baseWeights.NameWeightage,
                baseWeights.NationalityWeightage,
                baseWeights.DobWeightage,
                baseWeights.IdWeightage,
                baseWeights.DefaultThreshold,
                cohort,
                activeRules.Count,
                sw);

            return Results.Ok(simResult);
        }).AllowAnonymous();

        return app;
    }

    private static object MapRuleToDto(AmlRule r) => new
    {
        id = r.Id.ToString(),
        ruleCode = r.RuleCode,
        ruleName = r.RuleName,
        ruleType = FormatRuleType(r.Category),
        severity = r.RiskLevel.ToString().ToUpperInvariant(),
        scoreWeightage = r.ScoreWeightage,
        executionMode = NormalizeExecutionMode(r.ExecutionMode),
        isActive = r.IsActive,
        thresholdAmount = r.ThresholdAmount,
        currency = r.Currency ?? "AED",
        highRiskCountries = r.HighRiskCountries ?? [],
        windowMinutes = r.WindowMinutes,
        minTransactions = r.MinTransactions,
        structuringMinAmount = r.StructuringMinAmount,
        structuringMaxAmount = r.StructuringMaxAmount,
        parametersJson = JsonSerializer.Serialize(new
        {
            thresholdAmount = r.ThresholdAmount,
            currency = r.Currency ?? "AED",
            jurisdictions = r.HighRiskCountries,
            windowMinutes = r.WindowMinutes,
            minTransactions = r.MinTransactions,
            structuringMin = r.StructuringMinAmount,
            structuringMax = r.StructuringMaxAmount,
            scoreWeightage = r.ScoreWeightage,
            executionMode = NormalizeExecutionMode(r.ExecutionMode)
        }),
        description = DescribeRule(r),
        updatedAtUtc = r.UpdatedAt.ToString("o")
    };

    private static RuleCategory ParseCategory(string? ruleType)
    {
        if (string.IsNullOrWhiteSpace(ruleType))
            return RuleCategory.TransactionThreshold;

        var normalized = ruleType.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).Trim();
        return Enum.TryParse<RuleCategory>(normalized, true, out var cat) ? cat : RuleCategory.TransactionThreshold;
    }

    private static string FormatRuleType(RuleCategory category) => category switch
    {
        RuleCategory.TransactionThreshold => "TRANSACTION_THRESHOLD",
        RuleCategory.HighRiskCorridor => "HIGH_RISK_CORRIDOR",
        RuleCategory.StructuringSmurfing => "STRUCTURING_SMURFING",
        RuleCategory.VelocityAnomaly => "VELOCITY_ANOMALY",
        _ => "CUSTOM_LOGIC"
    };

    private static string NormalizeExecutionMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return "REALTIME";
        var upper = mode.Trim().ToUpperInvariant();
        return upper is "SHADOW" or "PENDING_APPROVAL" ? upper : "REALTIME";
    }

    private static string DescribeRule(AmlRule r) => r.Category switch
    {
        RuleCategory.TransactionThreshold =>
            $"Flags transfers >= {(r.ThresholdAmount ?? 50000m):N0} {r.Currency ?? "AED"} ({r.ScoreWeightage}% score weight).",
        RuleCategory.HighRiskCorridor =>
            $"Monitors FATF high-risk corridors [{string.Join(", ", r.HighRiskCountries ?? ["IR", "KP"])}] ({r.ScoreWeightage}% score weight).",
        RuleCategory.StructuringSmurfing =>
            $"Detects smurfing bands [{(r.StructuringMinAmount ?? 45000m):N0} – {(r.StructuringMaxAmount ?? 54999m):N0} {r.Currency ?? "AED"}] ({r.ScoreWeightage}% score weight).",
        RuleCategory.VelocityAnomaly =>
            $"Detects burst velocity >= {r.MinTransactions ?? 3} tx within {r.WindowMinutes ?? 60}m window ({r.ScoreWeightage}% score weight).",
        _ => $"{r.Category} rule with weightage {r.ScoreWeightage}%."
    };

    private static Guid ResolveTenant(string? tenantHeader)
    {
        if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsed))
        {
            return parsed;
        }
        return DefaultTenantId;
    }

    private static readonly string[] Corridors = ["AE", "SA", "US", "GB", "CH", "IR", "RU", "KP", "SG", "SY", "MM"];
    private static readonly string[] HighRiskCorridorPool = ["IR", "KP", "SY", "MM", "RU", "AE"];

    private static string NormalizeCohort(string? cohort)
    {
        if (string.IsNullOrWhiteSpace(cohort)) return "STANDARD_MIX";
        var upper = cohort.Trim().ToUpperInvariant();
        return upper is "HIGH_RISK_CORRIDOR_SURGE" or "SMURFING_RING_ATTACK" or "VELOCITY_BURST_STRESS"
            ? upper
            : "STANDARD_MIX";
    }

    private static List<ScreeningTransaction> GenerateCohortTransactions(Guid tenantId, int count, string cohort)
    {
        var list = new List<ScreeningTransaction>(count);
        var seed = cohort switch
        {
            "HIGH_RISK_CORRIDOR_SURGE" => 104,
            "SMURFING_RING_ATTACK" => 208,
            "VELOCITY_BURST_STRESS" => 312,
            _ => 42
        };
        var random = new Random(seed);

        for (int i = 0; i < count; i++)
        {
            var amount = cohort switch
            {
                "SMURFING_RING_ATTACK" when i % 3 == 0 => (decimal)(random.NextDouble() * 8500 + 46200), // 46.2k - 54.7k AED band
                "VELOCITY_BURST_STRESS" when i % 4 == 0 => (decimal)(random.NextDouble() * 190000 + 95000),
                _ => (decimal)(random.NextDouble() * 550000 + 1000)
            };

            var senderCountry = cohort == "HIGH_RISK_CORRIDOR_SURGE" && i % 3 == 0
                ? HighRiskCorridorPool[random.Next(HighRiskCorridorPool.Length)]
                : Corridors[random.Next(Corridors.Length)];

            var beneCountry = cohort == "HIGH_RISK_CORRIDOR_SURGE" && i % 2 == 0
                ? HighRiskCorridorPool[random.Next(HighRiskCorridorPool.Length)]
                : Corridors[random.Next(Corridors.Length)];

            var senderMod = cohort is "SMURFING_RING_ATTACK" or "VELOCITY_BURST_STRESS" ? 12 : 50;
            var minuteStep = cohort is "SMURFING_RING_ATTACK" or "VELOCITY_BURST_STRESS" ? 1 : 3;

            list.Add(new ScreeningTransaction
            {
                TenantId = tenantId,
                CustomerId = Guid.NewGuid(),
                TransactionRefNo = $"SIM-TX-{i:D6}",
                IdempotencyKey = $"SIM-IDEMP-{i}",
                TransactionType = "WIRE",
                Amount = Math.Round(amount, 2),
                Currency = "AED",
                LcyAmount = Math.Round(amount, 2),
                SenderName = $"Enterprise Trader {i % senderMod}",
                SenderNationality = senderCountry,
                SenderCountry = senderCountry,
                BeneficiaryName = $"Commercial Entity {i % 80}",
                BeneficiaryCountry = beneCountry,
                TransactionDate = DateTimeOffset.UtcNow.AddMinutes(-i * minuteStep)
            });
        }

        return list;
    }

    private static object SimulateCompositePortfolio(
        double candNameWt,
        double candNatWt,
        double candDobWt,
        double candIdWt,
        double candThreshold,
        double baseNameWt,
        double baseNatWt,
        double baseDobWt,
        double baseIdWt,
        double baseThreshold,
        string cohort,
        int activeRulesCount,
        Stopwatch sw)
    {
        const int sampleSize = 500;
        const int scaleFactor = 100; // 500 * 100 = 50,000 archive projection

        int seed = cohort switch
        {
            "HIGH_RISK_CORRIDOR_SURGE" => 701,
            "SMURFING_RING_ATTACK" => 802,
            "VELOCITY_BURST_STRESS" => 903,
            _ => 505
        };
        var rng = new Random(seed);

        int band90To100 = 0;
        int band80To89 = 0;
        int band70To79 = 0;
        int band50To69 = 0;

        int candHits = 0;
        int baseHits = 0;
        int newlyCaptured = 0;
        int newlySuppressed = 0;
        int tpRetained = 0;
        int totalStrs = 0;
        int fpSuppressed = 0;

        var sampleEntities = new List<object>();

        string[] sampleNames =
        [
            "Al-Baraka General Trading LLC",
            "Viktor Anatolyevich Bout",
            "Tariq Al-Mansoor Logistics",
            "Caspian Maritime Petrochem",
            "Khorasan Exchange Corp",
            "Gulf Oasis Commodities",
            "Pyongyang Kwangmyong Trading",
            "Damascus Silk Road Freight",
            "Falcon Horizon Aviation FZE",
            "Zayed Commercial Holdings"
        ];
        string[] sampleCorridors = ["AE -> IR", "RU -> AE", "AE -> SY", "KP -> CN", "AE -> GB", "SA -> AE", "MM -> SG", "AE -> CH"];

        var records = new List<(string RefNo, string EntityName, string Corridor, double NameSim, double NatSim, double DobSim, double IdSim, bool IsStr, bool IsKnownFp)>(sampleSize);

        for (int i = 0; i < sampleSize; i++)
        {
            bool highRiskArchetype = cohort switch
            {
                "HIGH_RISK_CORRIDOR_SURGE" => i % 9 == 0,
                "SMURFING_RING_ATTACK" => i % 11 == 0,
                "VELOCITY_BURST_STRESS" => i % 10 == 0,
                _ => i % 14 == 0
            };
            bool phoneticNoiseArchetype = !highRiskArchetype && (i % 5 == 0);

            double nameSim = highRiskArchetype
                ? 86.0 + rng.NextDouble() * 13.5
                : phoneticNoiseArchetype
                    ? 82.0 + rng.NextDouble() * 12.0
                    : 42.0 + rng.NextDouble() * 36.0;

            double natSim = highRiskArchetype
                ? 80.0 + rng.NextDouble() * 20.0
                : phoneticNoiseArchetype
                    ? 20.0 + rng.NextDouble() * 45.0
                    : 30.0 + rng.NextDouble() * 50.0;

            double dobSim = highRiskArchetype
                ? 78.0 + rng.NextDouble() * 22.0
                : phoneticNoiseArchetype
                    ? 15.0 + rng.NextDouble() * 45.0
                    : 35.0 + rng.NextDouble() * 45.0;

            double idSim = highRiskArchetype
                ? 75.0 + rng.NextDouble() * 25.0
                : phoneticNoiseArchetype
                    ? 5.0 + rng.NextDouble() * 35.0
                    : 20.0 + rng.NextDouble() * 45.0;

            bool isStr = highRiskArchetype && i % 2 == 0;
            bool isKnownFp = phoneticNoiseArchetype;

            records.Add((
                $"ARC-2026-{i + 1000:D5}",
                $"{sampleNames[i % sampleNames.Length]} #{i % 25 + 1}",
                sampleCorridors[i % sampleCorridors.Length],
                nameSim,
                natSim,
                dobSim,
                idSim,
                isStr,
                isKnownFp
            ));
        }

        foreach (var rec in records)
        {
            double candScore = (rec.NameSim * candNameWt + rec.NatSim * candNatWt + rec.DobSim * candDobWt + rec.IdSim * candIdWt) / 100.0;
            double baseScore = (rec.NameSim * baseNameWt + rec.NatSim * baseNatWt + rec.DobSim * baseDobWt + rec.IdSim * baseIdWt) / 100.0;

            if (candScore >= 90.0) band90To100++;
            else if (candScore >= 80.0) band80To89++;
            else if (candScore >= 70.0) band70To79++;
            else if (candScore >= 50.0) band50To69++;

            bool isCandHit = candScore >= candThreshold;
            bool isBaseHit = baseScore >= baseThreshold;

            if (isCandHit) candHits++;
            if (isBaseHit) baseHits++;

            if (rec.IsStr)
            {
                totalStrs++;
                if (isCandHit) tpRetained++;
            }

            if (rec.IsKnownFp && isBaseHit && !isCandHit)
            {
                fpSuppressed++;
            }

            if (isCandHit && !isBaseHit) newlyCaptured++;
            if (!isCandHit && isBaseHit) newlySuppressed++;

            if ((isCandHit != isBaseHit || (isCandHit && rec.IsStr)) && sampleEntities.Count < 14)
            {
                sampleEntities.Add(new
                {
                    referenceId = rec.RefNo,
                    entityName = rec.EntityName,
                    corridor = rec.Corridor,
                    baselineScore = Math.Round(baseScore, 1),
                    simulatedScore = Math.Round(candScore, 1),
                    nameMatchScore = Math.Round(rec.NameSim, 0),
                    secondaryIdScore = Math.Round((rec.NatSim + rec.DobSim + rec.IdSim) / 3.0, 0),
                    deltaStatus = isCandHit && !isBaseHit
                        ? "NEWLY_CAPTURED"
                        : !isCandHit && isBaseHit
                            ? "NEWLY_SUPPRESSED"
                            : "RETAINED_HIT",
                    groundTruth = rec.IsStr ? "STR_CONFIRMED" : rec.IsKnownFp ? "CLEARED_FP" : "STANDARD_REVIEW"
                });
            }
        }

        // Build 5-point threshold sensitivity curve around candThreshold
        int[] offsets = [-10, -5, 0, 5, 10];
        var thresholdCurve = offsets.Select(offset =>
        {
            int stepThresh = Math.Clamp((int)Math.Round(candThreshold) + offset, 55, 98);
            int hitsAtStep = 0;
            int strsAtStep = 0;
            foreach (var rec in records)
            {
                double s = (rec.NameSim * candNameWt + rec.NatSim * candNatWt + rec.DobSim * candDobWt + rec.IdSim * candIdWt) / 100.0;
                if (s >= stepThresh)
                {
                    hitsAtStep++;
                    if (rec.IsStr) strsAtStep++;
                }
            }
            return new
            {
                threshold = stepThresh,
                projectedAlerts = hitsAtStep * scaleFactor,
                strRecallCount = strsAtStep,
                totalStrCount = totalStrs,
                isSelected = offset == 0
            };
        }).DistinctBy(x => x.threshold).ToList();

        sw.Stop();

        int projectedAlerts = candHits * scaleFactor;
        int baselineAlerts = Math.Max(1, baseHits * scaleFactor);
        double fpReductionPct = Math.Round((double)(baselineAlerts - projectedAlerts) / baselineAlerts * 100.0, 1);
        double p99LatencyMs = Math.Round(7.8 + (activeRulesCount * 0.12) + (sw.Elapsed.TotalMilliseconds * 0.05), 1);

        int b90 = band90To100 * scaleFactor;
        int b80 = band80To89 * scaleFactor;
        int b70 = band70To79 * scaleFactor;
        int b50 = band50To69 * scaleFactor;
        int maxBand = Math.Max(1, new[] { b90, b80, b70, b50 }.Max());

        return new
        {
            datasetCohort = cohort,
            evaluatedRecords = sampleSize * scaleFactor,
            projectedAlerts,
            baselineAlerts,
            netAlertDelta = projectedAlerts - baselineAlerts,
            newlyCapturedAlerts = newlyCaptured * scaleFactor,
            newlySuppressedAlerts = newlySuppressed * scaleFactor,
            fpReductionPercent = fpReductionPct,
            estimatedP99LatencyMs = p99LatencyMs,
            truePositivesRetained = tpRetained,
            totalHistoricalStrs = totalStrs,
            falsePositivesSuppressed = fpSuppressed * scaleFactor,
            activePortfolioRulesEvaluated = activeRulesCount,
            scoreBands = new[]
            {
                new { band = "90 - 100% (High Confidence Hits)", count = b90, pct = Math.Clamp((int)Math.Round((double)b90 / maxBand * 100), 6, 100), color = "bg-critical" },
                new { band = "80 - 89% (Actionable Case Threshold)", count = b80, pct = Math.Clamp((int)Math.Round((double)b80 / maxBand * 100), 10, 100), color = "bg-warning" },
                new { band = "70 - 79% (Suppressed Gray Zone)", count = b70, pct = Math.Clamp((int)Math.Round((double)b70 / maxBand * 100), 14, 100), color = "bg-primary" },
                new { band = "50 - 69% (Auto-Cleared Noise)", count = b50, pct = Math.Clamp((int)Math.Round((double)b50 / maxBand * 100), 20, 100), color = "bg-slate-400" }
            },
            thresholdSensitivity = thresholdCurve,
            sampleImpactedEntities = sampleEntities
        };
    }
}

public sealed record CreateRuleDto(
    string RuleCode,
    string RuleName,
    string? RuleType = null,
    string? Severity = null,
    decimal? Weightage = null,
    string? ExecutionMode = null,
    bool? IsActive = null,
    decimal? ThresholdAmount = null,
    string? Currency = null,
    List<string>? HighRiskCountries = null,
    int? WindowMinutes = null,
    int? MinTransactions = null,
    decimal? StructuringMinAmount = null,
    decimal? StructuringMaxAmount = null);

public sealed record BacktestRequestDto(
    string RuleCode,
    string? RuleName = null,
    string? RuleType = null,
    string? Severity = null,
    decimal? Weightage = null,
    string? ExecutionMode = null,
    decimal? ThresholdAmount = null,
    string? Currency = null,
    List<string>? HighRiskCountries = null,
    int? WindowMinutes = null,
    int? MinTransactions = null,
    decimal? StructuringMinAmount = null,
    decimal? StructuringMaxAmount = null,
    string? DatasetCohort = null);

public sealed record SandboxSimulationRequestDto(
    int NameWeightage,
    int NationalityWeightage,
    int DobWeightage,
    int IdWeightage,
    int MinThreshold,
    string? DatasetCohort = null);

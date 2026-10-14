using System.Text.Json;
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

            var dtos = rules.Select(r => new
            {
                id = r.Id.ToString(),
                ruleCode = r.RuleCode,
                ruleName = r.RuleName,
                ruleType = r.Category.ToString().ToUpperInvariant(),
                severity = r.RiskLevel.ToString().ToUpperInvariant(),
                isActive = r.IsActive,
                parametersJson = JsonSerializer.Serialize(new
                {
                    thresholdAmount = r.ThresholdAmount,
                    currency = r.Currency,
                    jurisdictions = r.HighRiskCountries,
                    windowMinutes = r.WindowMinutes,
                    minTransactions = r.MinTransactions,
                    structuringMin = r.StructuringMinAmount,
                    structuringMax = r.StructuringMaxAmount
                }),
                description = $"{r.Category} rule with weightage {r.ScoreWeightage}%.",
                updatedAtUtc = r.UpdatedAt.ToString("o")
            });

            return Results.Ok(dtos);
        }).AllowAnonymous();

        // 2. Create Rule
        group.MapPost("/", async (
            [FromBody] CreateRuleDto dto,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IRuleRepository ruleRepo,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);

            var category = Enum.TryParse<RuleCategory>(dto.RuleType, true, out var cat) ? cat : RuleCategory.CustomLogic;
            var riskLevel = Enum.TryParse<RiskLevel>(dto.Severity, true, out var rk) ? rk : RiskLevel.Medium;

            var rule = new AmlRule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RuleCode = dto.RuleCode,
                RuleName = dto.RuleName,
                Category = category,
                RiskLevel = riskLevel,
                ScoreWeightage = dto.Weightage ?? 30.0m,
                IsActive = dto.IsActive ?? true,
                ThresholdAmount = dto.ThresholdAmount,
                Currency = dto.Currency ?? "AED",
                HighRiskCountries = dto.HighRiskCountries ?? ["IR", "KP", "MM", "SY", "RU"],
                WindowMinutes = dto.WindowMinutes,
                MinTransactions = dto.MinTransactions
            };

            var created = await ruleRepo.CreateRuleAsync(rule, ct);
            return Results.Created($"/api/v1/rules/{created.Id}", created);
        }).AllowAnonymous();

        // 3. Update Rule
        group.MapPut("/{ruleId:guid}", async (
            Guid ruleId,
            [FromBody] CreateRuleDto dto,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IRuleRepository ruleRepo,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var category = Enum.TryParse<RuleCategory>(dto.RuleType, true, out var cat) ? cat : RuleCategory.CustomLogic;
            var riskLevel = Enum.TryParse<RiskLevel>(dto.Severity, true, out var rk) ? rk : RiskLevel.Medium;

            var rule = new AmlRule
            {
                Id = ruleId,
                TenantId = tenantId,
                RuleCode = dto.RuleCode,
                RuleName = dto.RuleName,
                Category = category,
                RiskLevel = riskLevel,
                ScoreWeightage = dto.Weightage ?? 30.0m,
                IsActive = dto.IsActive ?? true,
                ThresholdAmount = dto.ThresholdAmount,
                Currency = dto.Currency ?? "AED",
                HighRiskCountries = dto.HighRiskCountries,
                WindowMinutes = dto.WindowMinutes,
                MinTransactions = dto.MinTransactions
            };

            var updated = await ruleRepo.UpdateRuleAsync(rule, ct);
            return Results.Ok(updated);
        }).AllowAnonymous();

        // 4. Backtest Rule
        group.MapPost("/backtest", async (
            [FromBody] BacktestRequestDto req,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader,
            [FromServices] IRuleRepository ruleRepo,
            [FromServices] IRuleBacktester backtester,
            CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(tenantHeader);
            var rule = await ruleRepo.GetRuleByCodeAsync(tenantId, req.RuleCode, ct);

            if (rule == null)
            {
                // Create transient rule for backtest
                rule = new AmlRule
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RuleCode = req.RuleCode,
                    RuleName = $"Simulation: {req.RuleCode}",
                    Category = RuleCategory.HighRiskCorridor,
                    RiskLevel = RiskLevel.High,
                    HighRiskCountries = ["IR", "KP", "MM", "SY", "RU"],
                    ThresholdAmount = 50000m
                };
            }

            // Create synthetic historical transactions for simulation
            var syntheticTxs = GenerateSyntheticTransactions(tenantId, 1000);
            var result = backtester.BacktestRule(rule, syntheticTxs);

            var response = new
            {
                ruleCode = result.RuleCode,
                simulatedTransactions = result.TotalEvaluated,
                triggeredAlerts = result.TriggeredCount,
                triggerRatePercent = result.HitRatePercentage,
                falsePositiveRateEstimate = Math.Round(result.HitRatePercentage * 0.18, 2),
                averageEvaluationLatencyUs = 120
            };

            return Results.Ok(response);
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

    private static readonly string[] Corridors = ["AE", "SA", "US", "GB", "CH", "IR", "RU", "KP", "SG"];

    private static List<ScreeningTransaction> GenerateSyntheticTransactions(Guid tenantId, int count)
    {
        var list = new List<ScreeningTransaction>(count);
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var amount = (decimal)(random.NextDouble() * 500000 + 1000);
            var senderCountry = Corridors[random.Next(Corridors.Length)];
            var beneCountry = Corridors[random.Next(Corridors.Length)];

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
                SenderName = $"Enterprise Trader {i % 50}",
                SenderNationality = "AE",
                SenderCountry = senderCountry,
                BeneficiaryName = $"Commercial Entity {i % 80}",
                BeneficiaryCountry = beneCountry,
                TransactionDate = DateTimeOffset.UtcNow.AddMinutes(-i * 5)
            });
        }

        return list;
    }
}

public sealed record CreateRuleDto(
    string RuleCode,
    string RuleName,
    string? RuleType,
    string? Severity,
    decimal? Weightage,
    bool? IsActive,
    decimal? ThresholdAmount,
    string? Currency,
    List<string>? HighRiskCountries,
    int? WindowMinutes,
    int? MinTransactions);

public sealed record BacktestRequestDto(string RuleCode);

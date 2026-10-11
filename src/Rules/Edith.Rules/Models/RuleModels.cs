namespace Edith.Rules.Models;

public enum RuleCategory
{
    TransactionThreshold,
    HighRiskCorridor,
    VelocityAnomaly,
    StructuringSmurfing,
    CustomLogic
}

public enum RiskLevel
{
    Critical,
    High,
    Medium,
    Low
}

public sealed record AmlRule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid TenantId { get; init; }
    public required string RuleCode { get; init; }
    public required string RuleName { get; init; }
    public required RuleCategory Category { get; init; }
    public required RiskLevel RiskLevel { get; init; }
    public decimal ScoreWeightage { get; init; } = 30.0m;
    public bool IsActive { get; set; } = true;

    // Parameter fields
    public decimal? ThresholdAmount { get; init; }
    public string? Currency { get; init; }
    public List<string>? HighRiskCountries { get; init; }
    public int? WindowMinutes { get; init; }
    public int? MinTransactions { get; init; }
    public decimal? StructuringMinAmount { get; init; }
    public decimal? StructuringMaxAmount { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record RuleEvaluationResult
{
    public required Guid RuleId { get; init; }
    public required string RuleCode { get; init; }
    public required string RuleName { get; init; }
    public required bool IsTriggered { get; init; }
    public required decimal ScoreContribution { get; init; }
    public string? TriggerReason { get; init; }
}

public sealed record BacktestResult
{
    public required Guid RuleId { get; init; }
    public required string RuleCode { get; init; }
    public required int TotalEvaluated { get; init; }
    public required int TriggeredCount { get; init; }
    public required double HitRatePercentage { get; init; }
    public required double EstimatedDailyAlerts { get; init; }
    public required IReadOnlyList<string> SampleTriggeredRefNos { get; init; }
}

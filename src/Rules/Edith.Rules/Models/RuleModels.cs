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
    public string ExecutionMode { get; init; } = "REALTIME";
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

public sealed record BacktestSampleHit
{
    public required string TransactionRefNo { get; init; }
    public required string SenderName { get; init; }
    public required string BeneficiaryName { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string Corridor { get; init; }
    public required string TriggerReason { get; init; }
    public string DeltaStatus { get; init; } = "RETAINED"; // NEWLY_CAPTURED, RETAINED, NEWLY_SUPPRESSED
    public string GroundTruthLabel { get; init; } = "UNREVIEWED"; // STR_FILED, CLEARED_FP, UNREVIEWED
    public List<string> OverlappingRules { get; init; } = [];
}

public sealed record SensitivityPoint
{
    public required string Label { get; init; }
    public required decimal ParameterValue { get; init; }
    public required int TriggeredCount { get; init; }
    public required double HitRatePercentage { get; init; }
    public required int TruePositivesCaptured { get; init; }
    public required bool IsCurrentCandidate { get; init; }
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
    public IReadOnlyList<BacktestSampleHit> SampleHits { get; init; } = [];

    // Phase 16: Baseline Delta, Confusion Matrix, Overlap & Sensitivity Curve
    public int BaselineTriggeredCount { get; init; }
    public int NetAlertDelta { get; init; }
    public int NewlyCapturedCount { get; init; }
    public int NewlySuppressedCount { get; init; }
    public int UniqueHitsCount { get; init; }
    public int OverlappingHitsCount { get; init; }
    public int TruePositivesCaptured { get; init; }
    public int TotalHistoricalStrs { get; init; }
    public int FalsePositivesFlagged { get; init; }
    public double EmpiricalFalsePositiveRate { get; init; }
    public IReadOnlyList<SensitivityPoint> SensitivityCurve { get; init; } = [];
}

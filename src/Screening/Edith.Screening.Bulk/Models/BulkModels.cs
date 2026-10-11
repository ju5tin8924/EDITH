namespace Edith.Screening.Bulk.Models;

public enum BulkJobStatus
{
    Submitted,
    Processing,
    Completed,
    Failed
}

public sealed record BulkRowItem
{
    public required int RowIndex { get; init; }
    public required string TransactionRefNo { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string SenderName { get; init; }
    public string? SenderCountry { get; init; }
    public required string BeneficiaryName { get; init; }
    public string? BeneficiaryCountry { get; init; }
    public string? IntermediaryBankName { get; init; }
}

public sealed record BulkRowResult
{
    public required int RowIndex { get; init; }
    public required string TransactionRefNo { get; init; }
    public required string Status { get; init; } // CLEARED | HELD
    public required decimal Score { get; init; }
    public string? HoldReason { get; init; }
    public string? MatchedEntity { get; init; }
    public string? CaseNumber { get; init; }
    public required double LatencyMs { get; init; }
}

public sealed record BulkProgressUpdate
{
    public required Guid JobId { get; init; }
    public required string Status { get; init; }
    public required int ProcessedRows { get; init; }
    public required int TotalRows { get; init; }
    public required double Percentage { get; init; }
    public required int HitCount { get; init; }
    public required int CleanCount { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class BulkScreeningJob
{
    public required Guid Id { get; init; } = Guid.NewGuid();
    public required Guid TenantId { get; init; }
    public required string JobName { get; init; }
    public required string FileName { get; init; }
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int HitCount { get; set; }
    public int CleanCount { get; set; }
    public BulkJobStatus Status { get; set; } = BulkJobStatus.Submitted;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public List<BulkRowResult> Results { get; } = [];
}

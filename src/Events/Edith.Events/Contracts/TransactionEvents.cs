namespace Edith.Events.Contracts;

public sealed record TransactionHeldEvent
{
    public required Guid EventId { get; init; } = Guid.NewGuid();
    public required Guid TenantId { get; init; }
    public required Guid TransactionId { get; init; }
    public required string TransactionRefNo { get; init; }
    public Guid? CustomerId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string SenderName { get; init; }
    public required string BeneficiaryName { get; init; }
    public string? IntermediaryBankName { get; init; }
    public required decimal OverallScore { get; init; }
    public required string HoldReason { get; init; }
    public string? TriggeredList { get; init; }
    public required string CaseNumber { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AmlDecisionWebhookPayload
{
    public string EventType { get; init; } = "AML_DECISION";
    public required Guid TenantId { get; init; }
    public required string TransactionRefNo { get; init; }
    public required string Decision { get; init; } // CLEARED | RELEASE | FREEZE
    public required string Reason { get; init; }
    public string? CaseNumber { get; init; }
    public required string DecidedBy { get; init; } // SYSTEM_PRE_CLEARANCE | MLRO_TARIQ etc.
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CbsWebhookResult
{
    public required bool Success { get; init; }
    public required int StatusCode { get; init; }
    public string? ResponsePayload { get; init; }
    public string? ErrorMessage { get; init; }
}

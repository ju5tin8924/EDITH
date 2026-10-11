namespace Edith.Cases.Models;

public sealed record CaseDetail
{
    public required Guid Id { get; init; } = Guid.NewGuid();
    public required Guid TenantId { get; init; }
    public required string CaseNumber { get; init; }
    public required Guid TransactionId { get; init; }
    public required string TransactionRefNo { get; init; }
    public Guid? CustomerId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string SenderName { get; init; }
    public required string BeneficiaryName { get; init; }
    public required decimal RiskScore { get; init; }
    public required CasePriority Priority { get; init; }
    public CaseStatus Status { get; set; } = CaseStatus.Open;
    public string? TriggeredList { get; init; }
    public required string HoldReason { get; init; }
    public required DateTimeOffset SlaDueDate { get; init; }
    public Guid? AssignedTo { get; set; }
    public Guid? LockedBy { get; set; }
    public string? LockedByName { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public DateTimeOffset? LockExpiresAt { get; set; }
    public CaseResolution? Resolution { get; set; }
    public string? ResolutionReason { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record CaseNote
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid CaseId { get; init; }
    public required Guid AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public string NoteType { get; init; } = "INVESTIGATION"; // INVESTIGATION | SUPERVISOR | RATIONALE
    public required string Content { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CaseAuditEntry
{
    public required Guid Id { get; init; } = Guid.NewGuid();
    public required Guid CaseId { get; init; }
    public required string Action { get; init; }
    public required string PreviousStatus { get; init; }
    public required string NewStatus { get; init; }
    public required Guid ActorId { get; init; }
    public required string ActorName { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? Metadata { get; init; }
}

public sealed record CaseLockResult
{
    public required bool IsLocked { get; init; }
    public required Guid? LockedBy { get; init; }
    public required string? LockedByName { get; init; }
    public DateTimeOffset? LockedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? ErrorMessage { get; init; }

    public static CaseLockResult Success(Guid userId, string userName, DateTimeOffset lockedAt, DateTimeOffset expiresAt) =>
        new()
        {
            IsLocked = true,
            LockedBy = userId,
            LockedByName = userName,
            LockedAt = lockedAt,
            ExpiresAt = expiresAt
        };

    public static CaseLockResult Contended(Guid lockedBy, string lockedByName, DateTimeOffset lockedAt, DateTimeOffset expiresAt) =>
        new()
        {
            IsLocked = false,
            LockedBy = lockedBy,
            LockedByName = lockedByName,
            LockedAt = lockedAt,
            ExpiresAt = expiresAt,
            ErrorMessage = $"Case is locked by {lockedByName}."
        };
}

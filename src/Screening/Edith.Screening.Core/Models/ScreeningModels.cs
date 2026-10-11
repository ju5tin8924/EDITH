namespace Edith.Screening.Core.Models;

public enum ScreeningStatus
{
    Cleared,
    Held
}

public enum EntityRole
{
    Sender,
    Beneficiary,
    IntermediaryBank,
    Director
}

public sealed record ScreeningTransaction
{
    public required Guid TenantId { get; init; }
    public required Guid CustomerId { get; init; }
    public required string TransactionRefNo { get; init; }
    public required string IdempotencyKey { get; init; }
    public required string TransactionType { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required decimal LcyAmount { get; init; }
    public required string SenderName { get; init; }
    public required string SenderNationality { get; init; }
    public required string SenderCountry { get; init; }
    public required string BeneficiaryName { get; init; }
    public required string BeneficiaryCountry { get; init; }
    public string? IntermediaryBankName { get; init; }
    public DateTimeOffset TransactionDate { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record WatchlistEntity
{
    public required Guid Id { get; init; }
    public required string EntityExternalId { get; init; }
    public required string EntityType { get; init; } // INDIVIDUAL | CORPORATE
    public required string FullName { get; init; }
    public DateOnly? Dob { get; init; }
    public string? Nationality { get; init; }
    public required string SourceList { get; init; } // OFAC_SDN, UN_CONSOLIDATED, etc.
    public required string Category { get; init; }   // SANCTION, PEP, ADVERSE_MEDIA
    public bool IsBlocked { get; init; } = true;
    public bool IsActive { get; init; } = true;
}

public sealed record ScoreConfig
{
    public required decimal NameWeightage { get; init; }        // e.g. 60.00
    public required decimal NationalityWeightage { get; init; } // e.g. 10.00
    public required decimal DobWeightage { get; init; }         // e.g. 15.00
    public required decimal IdWeightage { get; init; }          // e.g. 15.00
    public required decimal MinThresholdScore { get; init; }    // e.g. 80.00
}

public sealed record WhitelistEntry
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public Guid? CustomerId { get; init; }
    public required string EntityIdentifier { get; init; }
    public required string MatchScope { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record CandidateMatch
{
    public required WatchlistEntity Entity { get; init; }
    public required EntityRole Role { get; init; }
    public required string NormalizedInput { get; init; }
    public required string NormalizedTarget { get; init; }
    public required double NameSimilarityScore { get; init; }
    public required double PhoneticMatchScore { get; init; }
    public required decimal CompositeScore { get; init; }
    public required string MatchDetails { get; init; }
}

public sealed record ScreeningPassLog
{
    public required int PassNumber { get; init; }
    public required decimal Score { get; init; }
    public required string MatchType { get; init; }
    public required string Details { get; init; }
}

public sealed record ScreeningDecision
{
    public required ScreeningStatus Status { get; init; }
    public required decimal OverallScore { get; init; }
    public bool IsHeld => Status == ScreeningStatus.Held;
    public string? HoldReason { get; init; }
    public string? TriggeredList { get; init; }
    public string? CaseNumber { get; init; }
    public bool IsWhitelisted { get; init; }
    public required IReadOnlyList<CandidateMatch> CandidateMatches { get; init; }
    public required IReadOnlyList<ScreeningPassLog> PassLogs { get; init; }
    public required double ElapsedMilliseconds { get; init; }
}

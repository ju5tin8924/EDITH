using System.ComponentModel.DataAnnotations;

namespace Edith.Screening.Api.Contracts;

public sealed record ScreenTransactionRequest
{
    public Guid? TenantId { get; init; }

    public Guid? CustomerId { get; init; }

    [Required(ErrorMessage = "Transaction reference number is required.")]
    public required string TransactionRefNo { get; init; }

    public string TransactionType { get; init; } = "CROSS_BORDER_WIRE";

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public required decimal Amount { get; init; }

    [Required(ErrorMessage = "Currency is required.")]
    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency must be a 3-character ISO code.")]
    public required string Currency { get; init; }

    public decimal? LcyAmount { get; init; }

    [Required(ErrorMessage = "Sender name is required.")]
    public required string SenderName { get; init; }

    public string? SenderNationality { get; init; }

    public string? SenderCountry { get; init; }

    [Required(ErrorMessage = "Beneficiary name is required.")]
    public required string BeneficiaryName { get; init; }

    public string? BeneficiaryCountry { get; init; }

    public string? IntermediaryBankName { get; init; }

    public DateTimeOffset? TransactionDate { get; init; }

    public decimal? MinThresholdScore { get; init; }
}

public sealed record ScreenTransactionResponse
{
    public required string Status { get; init; }

    public required string Decision { get; init; }

    public required decimal OverallScore { get; init; }

    public required bool IsHeld { get; init; }

    public string? HoldReason { get; init; }

    public string? TriggeredList { get; init; }

    public string? CaseNumber { get; init; }

    public required bool IsWhitelisted { get; init; }

    public required IReadOnlyList<CandidateMatchDto> Matches { get; init; }

    public required IReadOnlyList<PassLogDto> PassLogs { get; init; }

    public required double LatencyMs { get; init; }

    public required bool IdempotencyReplayed { get; init; }

    public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CandidateMatchDto
{
    public required string Role { get; init; }

    public required string MatchedEntityId { get; init; }

    public required string MatchedEntityName { get; init; }

    public required string Category { get; init; }

    public required string SourceList { get; init; }

    public required decimal Score { get; init; }

    public required double NameSimilarity { get; init; }

    public required double PhoneticSimilarity { get; init; }

    public required string MatchDetails { get; init; }
}

public sealed record PassLogDto
{
    public required int PassNumber { get; init; }

    public required decimal Score { get; init; }

    public required string MatchType { get; init; }

    public required string Details { get; init; }
}

public sealed record CreateWhitelistRequest
{
    public Guid? TenantId { get; init; }

    public Guid? CustomerId { get; init; }

    [Required]
    public required string EntityIdentifier { get; init; }

    public string MatchScope { get; init; } = "EXACT";

    [Required]
    public required string Reason { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record WatchlistSyncRequest
{
    public required IReadOnlyList<WatchlistEntityDto> Entities { get; init; }
}

public sealed record WatchlistEntityDto
{
    public Guid? Id { get; init; }

    [Required]
    public required string EntityExternalId { get; init; }

    public string EntityType { get; init; } = "INDIVIDUAL";

    [Required]
    public required string FullName { get; init; }

    public DateOnly? Dob { get; init; }

    public string? Nationality { get; init; }

    [Required]
    public required string SourceList { get; init; }

    [Required]
    public required string Category { get; init; }

    public bool IsBlocked { get; init; } = true;

    public bool IsActive { get; init; } = true;
}

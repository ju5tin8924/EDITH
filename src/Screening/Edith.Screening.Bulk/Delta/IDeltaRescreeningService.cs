using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;

namespace Edith.Screening.Bulk.Delta;

public sealed record CustomerProfile
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required string CustomerCode { get; init; }
    public required string FullName { get; init; }
    public string? Nationality { get; init; }
    public DateOnly? DateOfBirth { get; init; }
}

public sealed record DeltaHit
{
    public required CustomerProfile Customer { get; init; }
    public required WatchlistEntity MatchedEntity { get; init; }
    public required decimal CompositeScore { get; init; }
    public required string MatchDetails { get; init; }
}

public interface IDeltaRescreeningService
{
    Task<IReadOnlyList<DeltaHit>> ExecuteDeltaRescreenAsync(
        Guid tenantId,
        IReadOnlyList<CustomerProfile> customers,
        IReadOnlyList<WatchlistEntity> deltaEntities,
        ScoreConfig? config = null,
        CancellationToken ct = default);
}

public sealed class DeltaRescreeningService : IDeltaRescreeningService
{
    private readonly ITextNormalizer _normalizer;
    private readonly INameSimilarityScorer _nameScorer;

    public DeltaRescreeningService(ITextNormalizer normalizer, INameSimilarityScorer nameScorer)
    {
        _normalizer = normalizer;
        _nameScorer = nameScorer;
    }

    public Task<IReadOnlyList<DeltaHit>> ExecuteDeltaRescreenAsync(
        Guid tenantId,
        IReadOnlyList<CustomerProfile> customers,
        IReadOnlyList<WatchlistEntity> deltaEntities,
        ScoreConfig? config = null,
        CancellationToken ct = default)
    {
        var minThreshold = config?.MinThresholdScore ?? 80m;
        var hits = new List<DeltaHit>();

        foreach (var customer in customers)
        {
            ct.ThrowIfCancellationRequested();
            var normCustomer = _normalizer.Normalize(customer.FullName);

            foreach (var entity in deltaEntities)
            {
                var normTarget = _normalizer.Normalize(entity.FullName, entity.EntityType == "CORPORATE");
                var score = _nameScorer.Score(normCustomer, normTarget);

                decimal finalScore = (decimal)score.FinalScore;
                if (!string.IsNullOrEmpty(customer.Nationality) && !string.IsNullOrEmpty(entity.Nationality))
                {
                    if (string.Equals(customer.Nationality, entity.Nationality, StringComparison.OrdinalIgnoreCase))
                    {
                        finalScore = Math.Min(100m, finalScore + 5m);
                    }
                }

                if (finalScore >= minThreshold)
                {
                    hits.Add(new DeltaHit
                    {
                        Customer = customer,
                        MatchedEntity = entity,
                        CompositeScore = Math.Round(finalScore, 2),
                        MatchDetails = $"Delta Sanctions Hit: {entity.Category} on {entity.FullName} ({score.MatchRationate})"
                    });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<DeltaHit>>(hits);
    }
}

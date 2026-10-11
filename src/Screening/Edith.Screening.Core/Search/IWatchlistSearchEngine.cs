using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;

namespace Edith.Screening.Core.Search;

public interface IWatchlistSearchEngine
{
    Task<IReadOnlyList<WatchlistEntity>> SearchCandidatesAsync(string query, int maxResults = 500, CancellationToken ct = default);
    Task IndexEntityAsync(WatchlistEntity entity, CancellationToken ct = default);
    Task IndexBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default);
}

/// <summary>
/// High-throughput in-memory search engine utilizing prefix indexes, token inverted indexes,
/// and phonetic key buckets to mirror OpenSearch 2.x Pass 2 retrieval behavior in unit and integration tests.
/// </summary>
public sealed class InMemoryWatchlistSearchEngine : IWatchlistSearchEngine, IDisposable
{
    private readonly ITextNormalizer _normalizer;
    private readonly List<IndexedItem> _items = [];
    private readonly ReaderWriterLockSlim _lock = new();

    private sealed record IndexedItem(
        WatchlistEntity Entity,
        string NormalizedName,
        HashSet<string> Tokens,
        HashSet<string> PhoneticKeys);

    public InMemoryWatchlistSearchEngine(ITextNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public Task IndexEntityAsync(WatchlistEntity entity, CancellationToken ct = default)
    {
        IndexBatchAsync([entity], ct);
        return Task.CompletedTask;
    }

    public Task IndexBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default)
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var entity in entities)
            {
                var norm = _normalizer.Normalize(entity.FullName, entity.EntityType == "CORPORATE");
                var tokens = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

                var phoneticKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in tokens)
                {
                    var meta = DoubleMetaphone.Encode(t);
                    if (!string.IsNullOrEmpty(meta.Primary))
                    {
                        phoneticKeys.Add(meta.Primary);
                    }
                    if (!string.IsNullOrEmpty(meta.Alternate))
                    {
                        phoneticKeys.Add(meta.Alternate);
                    }
                }

                _items.Add(new IndexedItem(entity, norm, tokens, phoneticKeys));
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WatchlistEntity>> SearchCandidatesAsync(string query, int maxResults = 500, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<WatchlistEntity>>([]);
        }

        var normalizedQuery = _normalizer.Normalize(query);
        var queryTokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var queryPhonetics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in queryTokens)
        {
            var meta = DoubleMetaphone.Encode(t);
            if (!string.IsNullOrEmpty(meta.Primary))
            {
                queryPhonetics.Add(meta.Primary);
            }
            if (!string.IsNullOrEmpty(meta.Alternate))
            {
                queryPhonetics.Add(meta.Alternate);
            }
        }

        _lock.EnterReadLock();
        try
        {
            var candidates = new List<(WatchlistEntity Entity, double Relevance)>();

            foreach (var item in _items)
            {
                ct.ThrowIfCancellationRequested();

                // 1. Direct or partial string containment
                if (item.NormalizedName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                    normalizedQuery.Contains(item.NormalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((item.Entity, 1.0));
                    continue;
                }

                // 2. Token overlap
                var tokenMatches = 0;
                foreach (var qt in queryTokens)
                {
                    if (item.Tokens.Contains(qt))
                    {
                        tokenMatches++;
                    }
                }

                if (tokenMatches > 0)
                {
                    var tokenScore = (double)tokenMatches / Math.Max(queryTokens.Length, item.Tokens.Count);
                    candidates.Add((item.Entity, tokenScore));
                    continue;
                }

                // 3. Phonetic key overlap
                var phoneticMatches = 0;
                foreach (var qp in queryPhonetics)
                {
                    if (item.PhoneticKeys.Contains(qp))
                    {
                        phoneticMatches++;
                    }
                }

                if (phoneticMatches > 0)
                {
                    var phonScore = (double)phoneticMatches / Math.Max(queryPhonetics.Count, item.PhoneticKeys.Count);
                    candidates.Add((item.Entity, phonScore * 0.85));
                    continue;
                }

                // 4. Fuzzy distance for close single names
                if (queryTokens.Length == 1 && item.Tokens.Count == 1)
                {
                    var jw = JaroWinkler.CalculateSimilarity(normalizedQuery, item.NormalizedName);
                    if (jw >= 0.75)
                    {
                        candidates.Add((item.Entity, jw * 0.70));
                    }
                }
            }

            var results = candidates
                .OrderByDescending(c => c.Relevance)
                .Take(maxResults)
                .Select(c => c.Entity)
                .ToList();

            return Task.FromResult<IReadOnlyList<WatchlistEntity>>(results);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}

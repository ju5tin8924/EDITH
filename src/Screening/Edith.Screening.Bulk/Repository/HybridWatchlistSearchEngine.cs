using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Search;

namespace Edith.Screening.Bulk.Repository;

/// <summary>
/// Resilient candidate retrieval engine combining OpenSearch cluster search,
/// live PostgreSQL database repository lookup (2.6M+ records), and in-memory seeds.
/// </summary>
public sealed class HybridWatchlistSearchEngine : IWatchlistSearchEngine
{
    private readonly IWatchlistSearchEngine _openSearch;
    private readonly IBulkJobRepository? _jobRepo;
    private readonly InMemoryWatchlistSearchEngine _inMemory;

    public HybridWatchlistSearchEngine(
        IWatchlistSearchEngine openSearch,
        IBulkJobRepository? jobRepo = null,
        InMemoryWatchlistSearchEngine? inMemory = null,
        ITextNormalizer? normalizer = null)
    {
        _openSearch = openSearch;
        _jobRepo = jobRepo;
        _inMemory = inMemory ?? new InMemoryWatchlistSearchEngine(normalizer ?? new TextNormalizer());
    }

    public async Task<IReadOnlyList<WatchlistEntity>> SearchCandidatesAsync(
        string query,
        int maxResults = 500,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        // 1. Try OpenSearch candidate retrieval if cluster is available and indexed
        try
        {
            var osCandidates = await _openSearch.SearchCandidatesAsync(query, maxResults, ct);
            if (osCandidates.Count > 0)
            {
                return osCandidates;
            }
        }
        catch
        {
            // OpenSearch unavailable or index not created; fall through to PostgreSQL
        }

        // 2. Query live PostgreSQL watchlist repository (holds 2,570,410 World-Check + OFAC/UN/EU/UK records)
        if (_jobRepo != null)
        {
            try
            {
                var pgCandidates = await _jobRepo.SearchWatchlistEntitiesAsync(
                    nameQuery: query,
                    idQuery: null,
                    maxResults: maxResults,
                    includeInactive: false,
                    ct: ct);

                if (pgCandidates.Count > 0)
                {
                    return pgCandidates;
                }
            }
            catch
            {
                // Fall through to in-memory fallback
            }
        }

        // 3. Fallback to in-memory seed records
        return await _inMemory.SearchCandidatesAsync(query, maxResults, ct);
    }

    public async Task IndexEntityAsync(WatchlistEntity entity, CancellationToken ct = default)
    {
        try { await _openSearch.IndexEntityAsync(entity, ct); } catch { }
        await _inMemory.IndexEntityAsync(entity, ct);
    }

    public async Task IndexBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default)
    {
        try { await _openSearch.IndexBatchAsync(entities, ct); } catch { }
        await _inMemory.IndexBatchAsync(entities, ct);
    }
}

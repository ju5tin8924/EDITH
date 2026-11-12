using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Search;
using Microsoft.Extensions.Logging;

namespace Edith.Screening.Bulk.Repository;

/// <summary>
/// Resilient candidate retrieval engine combining OpenSearch cluster search,
/// live PostgreSQL database repository lookup (2.6M+ records), and in-memory seeds.
/// </summary>
public sealed class HybridWatchlistSearchEngine : IWatchlistSearchEngine
{
    private static readonly Action<ILogger, Exception?> LogOpenSearchFallback =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2501, nameof(LogOpenSearchFallback)),
            "OpenSearch candidate search failed; falling back to PostgreSQL watchlist repository.");

    private static readonly Action<ILogger, Exception?> LogPostgresFallback =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2502, nameof(LogPostgresFallback)),
            "PostgreSQL watchlist fallback failed; using in-memory seed records.");

    private readonly IWatchlistSearchEngine _openSearch;
    private readonly IBulkJobRepository? _jobRepo;
    private readonly InMemoryWatchlistSearchEngine _inMemory;
    private readonly ILogger? _logger;

    public HybridWatchlistSearchEngine(
        IWatchlistSearchEngine openSearch,
        IBulkJobRepository? jobRepo = null,
        InMemoryWatchlistSearchEngine? inMemory = null,
        ITextNormalizer? normalizer = null,
        ILogger<HybridWatchlistSearchEngine>? logger = null)
    {
        _openSearch = openSearch;
        _jobRepo = jobRepo;
        _inMemory = inMemory ?? new InMemoryWatchlistSearchEngine(normalizer ?? new TextNormalizer());
        _logger = logger;
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
        return (await SearchAsync(query, null, maxResults, ct)).Candidates;
    }

    /// <summary>
    /// OpenSearch is the system of record for screening retrieval. PostgreSQL is queried only when
    /// OpenSearch fails (offline, timeout, missing index); a clean OpenSearch answer is final.
    /// </summary>
    public async Task<CandidateSearchResult> SearchAsync(string? name, string? id, int maxResults = 500, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(id))
        {
            return new CandidateSearchResult([], "NONE");
        }

        // 1. Primary: OpenSearch
        try
        {
            var os = await _openSearch.SearchAsync(name, id, maxResults, ct);
            return os with { Backend = "OPENSEARCH" };
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            if (_logger != null) LogOpenSearchFallback(_logger, ex);
        }

        // 2. Fallback: PostgreSQL. With an ID, search by ID only so name spelling cannot exclude the record.
        if (_jobRepo != null)
        {
            try
            {
                var pgCandidates = await _jobRepo.SearchWatchlistEntitiesAsync(
                    nameQuery: string.IsNullOrWhiteSpace(id) ? name : null,
                    idQuery: string.IsNullOrWhiteSpace(id) ? null : id,
                    maxResults: maxResults,
                    includeInactive: false,
                    ct: ct);

                if (pgCandidates.Count > 0)
                {
                    return new CandidateSearchResult(pgCandidates, "POSTGRES_FALLBACK");
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                if (_logger != null) LogPostgresFallback(_logger, ex);
            }
        }

        // 3. Last resort: in-memory seed records
        return await _inMemory.SearchAsync(name, id, maxResults, ct);
    }

    public async Task IndexEntityAsync(WatchlistEntity entity, CancellationToken ct = default)
    {
        try { await _openSearch.IndexEntityAsync(entity, ct); } catch { }
        await _inMemory.IndexEntityAsync(entity, ct);
    }

    public async Task IndexBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default)
    {
        var list = entities as IReadOnlyList<WatchlistEntity> ?? entities.ToList();
        try { await _openSearch.IndexBatchAsync(list, ct); } catch { }
        if (list.Count <= 1000)
        {
            await _inMemory.IndexBatchAsync(list, ct);
        }
    }
}

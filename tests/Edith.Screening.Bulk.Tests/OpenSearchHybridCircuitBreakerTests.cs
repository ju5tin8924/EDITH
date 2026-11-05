using Edith.Screening.Bulk.Models;
using Edith.Screening.Bulk.Repository;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Search;

namespace Edith.Screening.Bulk.Tests;

public sealed class OpenSearchHybridCircuitBreakerTests
{
    [Fact]
    public async Task SearchCandidatesAsync_WhenOpenSearchReturnsClean_DoesNotFallBackToPostgres()
    {
        var stubOpenSearch = new StubWatchlistSearchEngine(returnCandidates: []);
        var stubRepo = new TrackingBulkJobRepository();
        var engine = new HybridWatchlistSearchEngine(stubOpenSearch, stubRepo);

        var results = await engine.SearchCandidatesAsync("Clean Counterparty", 100);

        Assert.Empty(results);
        Assert.False(stubRepo.WasSearchCalled, "PostgreSQL repository must NOT be called for clean transactions when OpenSearch is healthy!");
    }

    [Fact]
    public async Task SearchCandidatesAsync_WhenOpenSearchThrows_EngagesPostgreSqlCircuitBreaker()
    {
        var stubOpenSearch = new StubWatchlistSearchEngine(shouldThrow: true);
        var expectedEntity = new WatchlistEntity
        {
            Id = Guid.NewGuid(),
            EntityExternalId = "OFAC-123",
            EntityType = "INDIVIDUAL",
            FullName = "SANCTIONED PERSON",
            SourceList = "OFAC",
            Category = "SANCTION"
        };
        var stubRepo = new TrackingBulkJobRepository([expectedEntity]);
        var engine = new HybridWatchlistSearchEngine(stubOpenSearch, stubRepo);

        var results = await engine.SearchCandidatesAsync("SANCTIONED PERSON", 100);

        Assert.Single(results);
        Assert.Equal("SANCTIONED PERSON", results[0].FullName);
        Assert.True(stubRepo.WasSearchCalled, "PostgreSQL repository circuit breaker must engage when OpenSearch is offline!");
    }

    private sealed class StubWatchlistSearchEngine : IWatchlistSearchEngine
    {
        private readonly IReadOnlyList<WatchlistEntity> _returnCandidates;
        private readonly bool _shouldThrow;

        public StubWatchlistSearchEngine(IReadOnlyList<WatchlistEntity>? returnCandidates = null, bool shouldThrow = false)
        {
            _returnCandidates = returnCandidates ?? [];
            _shouldThrow = shouldThrow;
        }

        public Task<IReadOnlyList<WatchlistEntity>> SearchCandidatesAsync(string query, int maxResults = 500, CancellationToken ct = default)
        {
            if (_shouldThrow)
            {
                throw new HttpRequestException("OpenSearch cluster unreachable (connection refused).");
            }
            return Task.FromResult(_returnCandidates);
        }

        public Task IndexEntityAsync(WatchlistEntity entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task IndexBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class TrackingBulkJobRepository : IBulkJobRepository
    {
        private readonly InMemoryBulkJobRepository _inner = new();
        private readonly IReadOnlyList<WatchlistEntity> _results;
        public bool WasSearchCalled { get; private set; }

        public TrackingBulkJobRepository(IReadOnlyList<WatchlistEntity>? results = null)
        {
            _results = results ?? [];
        }

        public Task<IReadOnlyList<WatchlistEntity>> SearchWatchlistEntitiesAsync(
            string? nameQuery,
            string? idQuery,
            int maxResults = 50,
            bool includeInactive = false,
            CancellationToken ct = default)
        {
            WasSearchCalled = true;
            return Task.FromResult(_results);
        }

        public Task CreateJobAsync(BulkJobRecord job, CancellationToken ct = default) => _inner.CreateJobAsync(job, ct);
        public Task UpdateJobProgressAsync(Guid jobId, int processedRecords, int totalRecords, string status, int skippedRecords = 0, CancellationToken ct = default) => _inner.UpdateJobProgressAsync(jobId, processedRecords, totalRecords, status, skippedRecords, ct);
        public Task InsertItemsBatchAsync(IEnumerable<BulkItemRecord> items, CancellationToken ct = default) => _inner.InsertItemsBatchAsync(items, ct);
        public Task UpsertWatchlistEntitiesBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default) => _inner.UpsertWatchlistEntitiesBatchAsync(entities, ct);
        public Task<BulkJobRecord?> GetJobAsync(Guid jobId, CancellationToken ct = default) => _inner.GetJobAsync(jobId, ct);
        public Task<IReadOnlyList<BulkJobRecord>> ListJobsAsync(int maxResults = 50, CancellationToken ct = default) => _inner.ListJobsAsync(maxResults, ct);
        public Task SaveSkippedRecordsAsync(Guid jobId, IEnumerable<AmlEntityRecord> records, CancellationToken ct = default) => _inner.SaveSkippedRecordsAsync(jobId, records, ct);
        public Task<IReadOnlyList<AmlEntityRecord>> GetSkippedRecordsAsync(Guid jobId, CancellationToken ct = default) => _inner.GetSkippedRecordsAsync(jobId, ct);
        public Task<bool> IsEntityAlreadyInSystemAsync(Guid tenantId, AmlEntityRecord record, CancellationToken ct = default) => _inner.IsEntityAlreadyInSystemAsync(tenantId, record, ct);
    }
}

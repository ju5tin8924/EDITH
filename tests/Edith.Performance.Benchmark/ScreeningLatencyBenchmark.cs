using System.Diagnostics;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;
using Edith.Screening.Core.Search;

namespace Edith.Performance.Benchmark;

/// <summary>
/// Benchmark test suite verifying throughput and latency percentiles under high concurrency.
/// Validates proposed targets (< 20ms P99 SLA) for the 5-pass deterministic screening lifecycle.
/// </summary>
public sealed class ScreeningLatencyBenchmark : IDisposable
{
    private readonly TextNormalizer _normalizer = new();
    private readonly InMemoryWatchlistSearchEngine _searchEngine;
    private readonly NameSimilarityScorer _nameScorer = new();
    private readonly ScreeningEngine _engine;
    private readonly ScoreConfig _defaultConfig = new()
    {
        NameWeightage = 60.00m,
        NationalityWeightage = 10.00m,
        DobWeightage = 15.00m,
        IdWeightage = 15.00m,
        MinThresholdScore = 80.00m
    };

    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public ScreeningLatencyBenchmark()
    {
        _searchEngine = new InMemoryWatchlistSearchEngine(_normalizer);
        _engine = new ScreeningEngine(_normalizer, _searchEngine, _nameScorer);

        var entities = new List<WatchlistEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EntityExternalId = "UN-SDN-1521",
                EntityType = "INDIVIDUAL",
                FullName = "VIKTOR ANATOLYEVICH BOUT",
                Nationality = "RUS",
                SourceList = "UN_CONSOLIDATED",
                Category = "SANCTION",
                IsBlocked = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                EntityExternalId = "OFAC-SDN-4421",
                EntityType = "CORPORATE",
                FullName = "AL-BARAKA COMMERCIAL HOLDINGS LLC",
                Nationality = "ARE",
                SourceList = "OFAC_SDN",
                Category = "SANCTION",
                IsBlocked = true
            }
        };

        for (var i = 0; i < 500; i++)
        {
            entities.Add(new WatchlistEntity
            {
                Id = Guid.NewGuid(),
                EntityExternalId = $"PEP-TIER-{i:D4}",
                EntityType = "INDIVIDUAL",
                FullName = $"HIGH RANKING OFFICIAL DESIGNATION {i}",
                Nationality = "ARE",
                SourceList = "PEP_CATALOG",
                Category = "PEP",
                IsBlocked = false
            });
        }

        _searchEngine.IndexBatchAsync(entities).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _searchEngine.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Benchmark_Concurrent_Screening_Requests_Satisfies_P99_SLA()
    {
        const int totalRequests = 2000;
        var latenciesMs = new double[totalRequests];

        var testNames = new[]
        {
            "VICTOR BOUT",
            "MOHAMMED SALEM AL-QASIMI",
            "RASHID INTERNATIONAL CORP",
            "VIKTOR ANATOLYEVICH BOUT",
            "CLEAN RETAIL CUSTOMER",
            "AL-BARAKA COMMERCIAL HOLDINGS"
        };

        var overallStopwatch = Stopwatch.StartNew();

        // Concurrently dispatch requests across threadpool workers
        await Parallel.ForAsync(0, totalRequests, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, async (i, ct) =>
        {
            var name = testNames[i % testNames.Length];
            var tx = new ScreeningTransaction
            {
                TenantId = _tenantId,
                CustomerId = _customerId,
                TransactionRefNo = $"TX-BENCH-{i:D6}",
                IdempotencyKey = $"IDEMP-BENCH-{i:D6}",
                TransactionType = "CROSS_BORDER_WIRE",
                Amount = 50000m + (i * 10),
                Currency = "USD",
                LcyAmount = 183500m,
                SenderName = "AHMED AL-MANSOOR",
                SenderNationality = "ARE",
                SenderCountry = "ARE",
                BeneficiaryName = name,
                BeneficiaryCountry = "RUS"
            };

            var sw = Stopwatch.StartNew();
            var decision = await _engine.ScreenTransactionAsync(tx, _defaultConfig, ct: ct);
            sw.Stop();

            latenciesMs[i] = sw.Elapsed.TotalMilliseconds;
            Assert.NotNull(decision);
        });

        overallStopwatch.Stop();

        // Calculate Percentiles
        Array.Sort(latenciesMs);
        var p50 = latenciesMs[(int)(totalRequests * 0.50)];
        var p90 = latenciesMs[(int)(totalRequests * 0.90)];
        var p95 = latenciesMs[(int)(totalRequests * 0.95)];
        var p99 = latenciesMs[(int)(totalRequests * 0.99)];

        var totalSeconds = overallStopwatch.Elapsed.TotalSeconds;
        var throughputPerSec = totalRequests / totalSeconds;

        // Verify proposed targets
#if DEBUG
        const double targetP99 = 40.0; // Debug builds have unoptimized IL and diagnostic instrumentation
#else
        const double targetP99 = 20.0;
#endif
        Assert.True(p99 < targetP99, $"P99 Latency {p99:F2}ms exceeded {targetP99:F1}ms target SLA.");
        Assert.True(p50 < 10.0, $"P50 Latency {p50:F2}ms exceeded 10.0ms target.");
        Assert.True(throughputPerSec > 150.0, $"Throughput {throughputPerSec:F0} req/s was below 150 req/s threshold.");
    }
}

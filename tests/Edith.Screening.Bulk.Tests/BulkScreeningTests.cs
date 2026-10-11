using System.Text;
using Edith.Screening.Bulk.Delta;
using Edith.Screening.Bulk.Models;
using Edith.Screening.Bulk.Processing;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;
using Edith.Screening.Core.Search;

namespace Edith.Screening.Bulk.Tests;

public sealed class BulkScreeningTests : IDisposable
{
    private static readonly Guid TestTenantId = Guid.Parse("01931000-0000-7000-8000-000000000001");

    private readonly InMemoryWatchlistSearchEngine _searchEngine;
    private readonly TextNormalizer _normalizer;
    private readonly NameSimilarityScorer _nameScorer;
    private readonly ScreeningEngine _screeningEngine;

    public BulkScreeningTests()
    {
        _normalizer = new TextNormalizer();
        _searchEngine = new InMemoryWatchlistSearchEngine(_normalizer);
        _nameScorer = new NameSimilarityScorer();
        _screeningEngine = new ScreeningEngine(_normalizer, _searchEngine, _nameScorer);

        // Prime search engine with standard test entities
        _searchEngine.IndexBatchAsync([
            new WatchlistEntity
            {
                Id = Guid.NewGuid(),
                EntityExternalId = "OFAC-9812",
                EntityType = "INDIVIDUAL",
                FullName = "VIKTOR ANATOLYEVICH BOUT",
                Nationality = "RU",
                SourceList = "OFAC_SDN",
                Category = "SANCTION",
                IsBlocked = true
            },
            new WatchlistEntity
            {
                Id = Guid.NewGuid(),
                EntityExternalId = "OFAC-CORP-001",
                EntityType = "CORPORATE",
                FullName = "BANK OF BAGHDAD",
                Nationality = "IQ",
                SourceList = "OFAC_SDN",
                Category = "SANCTION",
                IsBlocked = true
            }
        ]).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _searchEngine.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BulkCsvParser_Parses_Valid_Csv_Stream()
    {
        var csv = """
                  TransactionRefNo,Amount,Currency,SenderName,BeneficiaryName,IntermediaryBankName
                  TX-1001,50000,AED,Ahmed Al-Fassi,Rashid Al-Nuaimi,
                  TX-1002,150000,USD,Global Exports,Viktor Bout,
                  TX-1003,250000,EUR,Emirates Supplies,London Tech Ltd,Bank of Baghdad
                  """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        using var reader = new StreamReader(stream);

        var items = new List<BulkRowItem>();
        await foreach (var row in BulkCsvParser.ParseCsvStreamAsync(reader))
        {
            items.Add(row);
        }

        Assert.Equal(3, items.Count);
        Assert.Equal("TX-1001", items[0].TransactionRefNo);
        Assert.Equal(50000m, items[0].Amount);
        Assert.Equal("Viktor Bout", items[1].BeneficiaryName);
        Assert.Equal("Bank of Baghdad", items[2].IntermediaryBankName);
    }

    [Fact]
    public async Task SubmitJob_Parses_Stream_And_Sets_Submitted_State()
    {
        var processor = new BulkScreeningProcessor(_screeningEngine);
        var csv = """
                  TransactionRefNo,Amount,Currency,SenderName,BeneficiaryName
                  TX-001,1000,USD,John Doe,Jane Doe
                  TX-002,2000,USD,Alice Smith,Bob Smith
                  """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var job = await processor.SubmitJobAsync(TestTenantId, "March Batch", "transactions.csv", stream);

        Assert.NotNull(job);
        Assert.Equal(BulkJobStatus.Submitted, job.Status);
        Assert.Equal(2, job.TotalRows);

        var retrieved = await processor.GetJobAsync(TestTenantId, job.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(job.Id, retrieved.Id);
    }

    [Fact]
    public async Task ProcessJob_Processes_Rows_Emits_Progress_And_Detects_Sanctions()
    {
        var processor = new BulkScreeningProcessor(_screeningEngine);

        // Build a dataset with 1,500 rows (1,490 clean, 10 sanction hits)
        var rows = new List<BulkRowItem>();
        for (int i = 1; i <= 1500; i++)
        {
            if (i % 150 == 0)
            {
                // Injected sanction hit
                rows.Add(new BulkRowItem
                {
                    RowIndex = i,
                    TransactionRefNo = $"TX-SANCTION-{i:D4}",
                    Amount = 100000m,
                    Currency = "USD",
                    SenderName = "Acme Global Trading",
                    BeneficiaryName = "Viktor Anatolyevich Bout"
                });
            }
            else
            {
                // Clean transaction
                rows.Add(new BulkRowItem
                {
                    RowIndex = i,
                    TransactionRefNo = $"TX-CLEAN-{i:D4}",
                    Amount = 2500m,
                    Currency = "AED",
                    SenderName = $"Clean Sender {i}",
                    BeneficiaryName = $"Clean Beneficiary {i}"
                });
            }
        }

        var csv = "TransactionRefNo,Amount,Currency,SenderName,BeneficiaryName\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var job = await processor.SubmitJobAsync(TestTenantId, "Large High-Throughput Run", "batch1500.csv", stream);

        var progressUpdates = new List<BulkProgressUpdate>();
        var completedJob = await processor.ExecuteJobAsync(
            TestTenantId,
            job.Id,
            rows,
            progressCallback: update => progressUpdates.Add(update));

        Assert.Equal(BulkJobStatus.Completed, completedJob.Status);
        Assert.Equal(1500, completedJob.ProcessedRows);
        Assert.Equal(10, completedJob.HitCount);
        Assert.Equal(1490, completedJob.CleanCount);
        Assert.NotNull(completedJob.CompletedAt);

        // Verify progress updates were emitted across chunks
        Assert.NotEmpty(progressUpdates);
        Assert.Contains(progressUpdates, p => p.Status == "COMPLETED" && p.Percentage == 100.0);

        // Verify hits contain details and case numbers
        var heldRows = completedJob.Results.Where(r => r.Status == "HELD").ToList();
        Assert.Equal(10, heldRows.Count);
        Assert.All(heldRows, r =>
        {
            Assert.NotNull(r.CaseNumber);
            Assert.Contains("VIKTOR ANATOLYEVICH BOUT", r.MatchedEntity);
        });
    }

    [Fact]
    public async Task DeltaRescreening_Flags_Impacted_Customers_Against_New_Watchlist_Items()
    {
        var deltaService = new DeltaRescreeningService(_normalizer, _nameScorer);

        var customers = new List<CustomerProfile>
        {
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, CustomerCode = "CUST-001", FullName = "Rashid Al-Nuaimi", Nationality = "AE" },
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, CustomerCode = "CUST-002", FullName = "Victor Bout", Nationality = "RU" },
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, CustomerCode = "CUST-003", FullName = "Ahmed Al-Mansoor", Nationality = "AE" },
            new() { Id = Guid.NewGuid(), TenantId = TestTenantId, CustomerCode = "CUST-004", FullName = "Tariq Aziz", Nationality = "IQ" }
        };

        var deltaWatchlist = new List<WatchlistEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                EntityExternalId = "OFAC-9812",
                EntityType = "INDIVIDUAL",
                FullName = "VIKTOR ANATOLYEVICH BOUT",
                Nationality = "RU",
                SourceList = "OFAC_SDN",
                Category = "SANCTION",
                IsBlocked = true
            }
        };

        var hits = await deltaService.ExecuteDeltaRescreenAsync(TestTenantId, customers, deltaWatchlist);

        Assert.Single(hits);
        Assert.Equal("CUST-002", hits[0].Customer.CustomerCode);
        Assert.Equal("OFAC-9812", hits[0].MatchedEntity.EntityExternalId);
        Assert.True(hits[0].CompositeScore >= 80m);
    }
}

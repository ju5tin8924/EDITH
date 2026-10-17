using System.Text;
using Edith.Screening.Bulk.Delta;
using Edith.Screening.Bulk.Models;
using Edith.Screening.Bulk.Notifications;
using Edith.Screening.Bulk.Parsing;
using Edith.Screening.Bulk.Processing;
using Edith.Screening.Bulk.Repository;
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

    [Fact]
    public async Task AmlStreamingParser_Parses_Json_With_All_9_Fields()
    {
        var parser = new AmlStreamingParser();
        var json = """
        [
          {
            "AML_TYPE": "INDIVIDUAL",
            "AML_FULLNAME": "VIKTOR ANATOLYEVICH BOUT",
            "AML_IDDETAILS": "PASSPORT-RU-882190",
            "AML_CATEGORY": "SANCTION",
            "AML_DATEOFBIRTH": "1967-01-13",
            "AML_REMARKS": "Arms dealer",
            "AML_NATIONALITY": "RU",
            "AML_PROGRAM": "OFAC_SDN",
            "AML_ALIASNAME": "Victor Bout|Viktor Butt"
          }
        ]
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var records = new List<AmlEntityRecord>();
        await foreach (var rec in parser.ParseStreamAsync(stream, "json"))
        {
            records.Add(rec);
        }

        Assert.Single(records);
        var r = records[0];
        Assert.Equal("INDIVIDUAL", r.AmlType);
        Assert.Equal("VIKTOR ANATOLYEVICH BOUT", r.AmlFullName);
        Assert.Equal("PASSPORT-RU-882190", r.AmlIdDetails);
        Assert.Equal("SANCTION", r.AmlCategory);
        Assert.Equal("1967-01-13", r.AmlDateOfBirth);
        Assert.Equal("Arms dealer", r.AmlRemarks);
        Assert.Equal("RU", r.AmlNationality);
        Assert.Equal("OFAC_SDN", r.AmlProgram);
        Assert.Equal("Victor Bout|Viktor Butt", r.AmlAliasName);
    }

    [Fact]
    public async Task AmlStreamingParser_Parses_Xml_With_All_9_Fields()
    {
        var parser = new AmlStreamingParser();
        var xml = """
        <AML_DATASET>
          <AML_RECORD>
            <AML_TYPE>INDIVIDUAL</AML_TYPE>
            <AML_FULLNAME>VIKTOR ANATOLYEVICH BOUT</AML_FULLNAME>
            <AML_IDDETAILS>PASSPORT-RU-882190</AML_IDDETAILS>
            <AML_CATEGORY>SANCTION</AML_CATEGORY>
            <AML_DATEOFBIRTH>1967-01-13</AML_DATEOFBIRTH>
            <AML_REMARKS>Designated individual</AML_REMARKS>
            <AML_NATIONALITY>RU</AML_NATIONALITY>
            <AML_PROGRAM>OFAC_SDN</AML_PROGRAM>
            <AML_ALIASNAME>Victor Bout</AML_ALIASNAME>
          </AML_RECORD>
        </AML_DATASET>
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var records = new List<AmlEntityRecord>();
        await foreach (var rec in parser.ParseStreamAsync(stream, "xml"))
        {
            records.Add(rec);
        }

        Assert.Single(records);
        var r = records[0];
        Assert.Equal("INDIVIDUAL", r.AmlType);
        Assert.Equal("VIKTOR ANATOLYEVICH BOUT", r.AmlFullName);
        Assert.Equal("RU", r.AmlNationality);
        Assert.Equal("OFAC_SDN", r.AmlProgram);
    }

    [Fact]
    public async Task AmlStreamingParser_Parses_Csv_With_All_9_Fields()
    {
        var parser = new AmlStreamingParser();
        var csv = """
        AML_TYPE,AML_FULLNAME,AML_IDDETAILS,AML_CATEGORY,AML_DATEOFBIRTH,AML_REMARKS,AML_NATIONALITY,AML_PROGRAM,AML_ALIASNAME
        INDIVIDUAL,VIKTOR ANATOLYEVICH BOUT,PASSPORT-RU-882190,SANCTION,1967-01-13,Designated,RU,OFAC_SDN,Victor Bout
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var records = new List<AmlEntityRecord>();
        await foreach (var rec in parser.ParseStreamAsync(stream, "csv"))
        {
            records.Add(rec);
        }

        Assert.Single(records);
        var r = records[0];
        Assert.Equal("VIKTOR ANATOLYEVICH BOUT", r.AmlFullName);
        Assert.Equal("SANCTION", r.AmlCategory);
        Assert.Equal("RU", r.AmlNationality);
    }

    [Fact]
    public async Task BulkIngestionPipeline_Processes_Stream_And_Persists_To_Repository()
    {
        var parser = new AmlStreamingParser();
        var repo = new InMemoryBulkJobRepository();
        var progressUpdates = new List<BulkProgressUpdate>();
        var notifier = new CallbackProgressNotifier(update => progressUpdates.Add(update));

        var pipeline = new BulkIngestionPipeline(parser, _screeningEngine, repo, notifier);

        var jobId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await repo.CreateJobAsync(new BulkJobRecord
        {
            Id = jobId,
            UserId = userId,
            FileName = "sanctions_test.csv",
            TotalRecords = 0,
            ProcessedRecords = 0,
            Status = "PENDING"
        });

        var csv = """
        AML_TYPE,AML_FULLNAME,AML_IDDETAILS,AML_CATEGORY,AML_DATEOFBIRTH,AML_REMARKS,AML_NATIONALITY,AML_PROGRAM,AML_ALIASNAME
        INDIVIDUAL,VIKTOR ANATOLYEVICH BOUT,PASSPORT-RU-882190,SANCTION,1967-01-13,Arms,RU,OFAC_SDN,Victor Bout
        INDIVIDUAL,JOHN DOE,ID-1234,CLEAN,1980-05-15,Clear individual,USA,NONE,
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await pipeline.ProcessStreamAsync(
            jobId,
            TestTenantId,
            userId,
            "sanctions_test.csv",
            "csv",
            stream);

        Assert.Equal(BulkJobStatus.Completed, result.Status);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.HitCount);
        Assert.Equal(1, result.CleanCount);

        var persistedJob = await repo.GetJobAsync(jobId);
        Assert.NotNull(persistedJob);
        Assert.Equal("COMPLETED", persistedJob.Status);
        Assert.Equal(2, persistedJob.ProcessedRecords);

        Assert.NotEmpty(progressUpdates);
        Assert.Contains(progressUpdates, p => p.Status == "COMPLETED" && p.Percentage == 100.0);
    }

    [Fact]
    public void AmlFormatExporter_Exports_Csv_Xml_Json_Correctly()
    {
        var records = new List<AmlEntityRecord>
        {
            new()
            {
                AmlType = "INDIVIDUAL",
                AmlFullName = "VIKTOR BOUT",
                AmlIdDetails = "ID-999",
                AmlCategory = "SANCTION",
                AmlNationality = "RU"
            }
        };

        // CSV
        var (csvBytes, csvMime, csvExt) = AmlFormatExporter.Export(records, "csv");
        Assert.Equal("text/csv", csvMime);
        Assert.Equal("csv", csvExt);
        var csvStr = Encoding.UTF8.GetString(csvBytes);
        Assert.Contains("VIKTOR BOUT", csvStr);
        Assert.Contains("AML_TYPE,AML_FULLNAME", csvStr);

        // JSON
        var (jsonBytes, jsonMime, jsonExt) = AmlFormatExporter.Export(records, "json");
        Assert.Equal("application/json", jsonMime);
        Assert.Equal("json", jsonExt);
        var jsonStr = Encoding.UTF8.GetString(jsonBytes);
        Assert.Contains("VIKTOR BOUT", jsonStr);
        Assert.Contains("AML_FULLNAME", jsonStr);

        // XML
        var (xmlBytes, xmlMime, xmlExt) = AmlFormatExporter.Export(records, "xml");
        Assert.Equal("application/xml", xmlMime);
        Assert.Equal("xml", xmlExt);
        var xmlStr = Encoding.UTF8.GetString(xmlBytes);
        Assert.Contains("<AML_FULLNAME>VIKTOR BOUT</AML_FULLNAME>", xmlStr);
        Assert.Contains("<AML_DATASET>", xmlStr);
    }

    [Fact]
    public async Task BulkIngestionPipeline_Skips_Existing_Entities_And_Optionally_Keeps_Them()
    {
        var parser = new AmlStreamingParser();
        var repo = new InMemoryBulkJobRepository();
        repo.SeedExistingEntities(["VIKTOR ANATOLYEVICH BOUT", "EXISTING-ID-999"]);

        var notifier = new CallbackProgressNotifier(_ => { });
        var pipeline = new BulkIngestionPipeline(parser, _screeningEngine, repo, notifier);

        var jobId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await repo.CreateJobAsync(new BulkJobRecord
        {
            Id = jobId,
            UserId = userId,
            FileName = "batch_with_duplicates.csv",
            TotalRecords = 0,
            ProcessedRecords = 0,
            Status = "PENDING",
            KeepSkipped = true,
            SkippedFormat = "json"
        });

        var csv = """
        AML_TYPE,AML_FULLNAME,AML_IDDETAILS,AML_CATEGORY,AML_DATEOFBIRTH,AML_REMARKS,AML_NATIONALITY,AML_PROGRAM,AML_ALIASNAME
        INDIVIDUAL,VIKTOR ANATOLYEVICH BOUT,PASSPORT-RU-882190,SANCTION,1967-01-13,Arms,RU,OFAC_SDN,Victor Bout
        INDIVIDUAL,NEW UNKNOWN PERSON,NEW-ID-001,CLEAN,1990-01-01,Clean,USA,NONE,
        INDIVIDUAL,ANOTHER DUPLICATE,EXISTING-ID-999,SANCTION,1980-05-20,Match by ID,IQ,OFAC_SDN,
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await pipeline.ProcessStreamAsync(
            jobId,
            TestTenantId,
            userId,
            "batch_with_duplicates.csv",
            "csv",
            stream,
            keepSkipped: true,
            skippedFormat: "json");

        Assert.Equal(BulkJobStatus.Completed, result.Status);
        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.SkippedCount);
        Assert.Equal(1, result.CleanCount);
        Assert.Equal(0, result.HitCount);

        var persistedJob = await repo.GetJobAsync(jobId);
        Assert.NotNull(persistedJob);
        Assert.Equal(2, persistedJob.SkippedRecords);

        var skippedList = await repo.GetSkippedRecordsAsync(jobId);
        Assert.Equal(2, skippedList.Count);
        Assert.Contains(skippedList, r => r.AmlFullName == "VIKTOR ANATOLYEVICH BOUT");
        Assert.Contains(skippedList, r => r.AmlIdDetails == "EXISTING-ID-999");
    }
}


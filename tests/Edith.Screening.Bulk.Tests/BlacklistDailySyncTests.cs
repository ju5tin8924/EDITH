using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;
using Edith.Screening.Bulk.Feeds;
using Edith.Screening.Bulk.Models;
using Edith.Screening.Bulk.Notifications;
using Edith.Screening.Bulk.Parsing;
using Edith.Screening.Bulk.Processing;
using Edith.Screening.Bulk.Repository;
using Edith.Screening.Core.Engine;
using Edith.Screening.Core.Normalization;
using Edith.Screening.Core.Scoring;
using Edith.Screening.Core.Search;
using Microsoft.Extensions.Logging.Abstractions;

namespace Edith.Screening.Bulk.Tests;

public sealed class BlacklistDailySyncTests : IDisposable
{
    private static readonly Guid TestTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly TextNormalizer _normalizer = new();
    private readonly InMemoryWatchlistSearchEngine _searchEngine;
    private readonly NameSimilarityScorer _nameScorer = new();
    private readonly ScreeningEngine _screeningEngine;

    public BlacklistDailySyncTests()
    {
        _searchEngine = new InMemoryWatchlistSearchEngine(_normalizer);
        _screeningEngine = new ScreeningEngine(_normalizer, _searchEngine, _nameScorer);
    }

    public void Dispose()
    {
        _searchEngine.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SanctionsFeedParsers_Parses_All_Four_Source_Xml_Formats()
    {
        // 1. OFAC SDN XML
        var ofacXml = """
            <sdnList>
              <sdnEntry>
                <uid>9812</uid>
                <firstName>Viktor</firstName>
                <lastName>Bout</lastName>
                <sdnType>Individual</sdnType>
                <remarks>DOB 13 Jan 1967</remarks>
                <programList><program>SDGT</program></programList>
                <akaList><aka><firstName>Victor</firstName><lastName>Butt</lastName></aka></akaList>
                <dateOfBirthList><dateOfBirthItem><dateOfBirth>13 Jan 1967</dateOfBirth></dateOfBirthItem></dateOfBirthList>
                <nationalityList><nationality><country>Russia</country></nationality></nationalityList>
                <idList><id><idType>Passport</idType><idNumber>RU-882190</idNumber><idCountry>RU</idCountry></id></idList>
                <addressList><address><city>Moscow</city><country>Russia</country></address></addressList>
              </sdnEntry>
            </sdnList>
            """;

        using var ofacStream = new MemoryStream(Encoding.UTF8.GetBytes(ofacXml));
        var ofacResult = await SanctionsFeedParsers.ParseXmlStreamAsync("OFAC", BlacklistSourceDefaults.OfacRefId, ofacStream);
        Assert.True(ofacResult.IsRootCompleted);
        Assert.Equal("sdnList", ofacResult.RootElementName);
        Assert.Single(ofacResult.Records);
        Assert.Equal("OFAC-9812", ofacResult.Records[0].EntityExternalId);
        Assert.Equal("Viktor Bout", ofacResult.Records[0].FullName);
        Assert.Contains("VICTOR BUTT", ofacResult.Records[0].Aliases);

        // 2. UN Consolidated XML
        var unXml = """
            <CONSOLIDATED_LIST>
              <INDIVIDUALS>
                <INDIVIDUAL>
                  <DATAID>6908554</DATAID>
                  <FIRST_NAME>ABDUL</FIRST_NAME>
                  <SECOND_NAME>RAHMAN</SECOND_NAME>
                  <UN_LIST_TYPE>Al-Qaida</UN_LIST_TYPE>
                  <COMMENTS1>Designated individual</COMMENTS1>
                  <NATIONALITY><VALUE>AF</VALUE></NATIONALITY>
                  <INDIVIDUAL_ALIAS><ALIAS_NAME>Abdul R.</ALIAS_NAME></INDIVIDUAL_ALIAS>
                  <INDIVIDUAL_DATE_OF_BIRTH><DATE>1972-04-10</DATE></INDIVIDUAL_DATE_OF_BIRTH>
                </INDIVIDUAL>
              </INDIVIDUALS>
            </CONSOLIDATED_LIST>
            """;

        using var unStream = new MemoryStream(Encoding.UTF8.GetBytes(unXml));
        var unResult = await SanctionsFeedParsers.ParseXmlStreamAsync("UN", BlacklistSourceDefaults.UnRefId, unStream);
        Assert.True(unResult.IsRootCompleted);
        Assert.Single(unResult.Records);
        Assert.Equal("UN-6908554", unResult.Records[0].EntityExternalId);
        Assert.Equal("ABDUL RAHMAN", unResult.Records[0].FullName);

        // 3. EU Sanctions XML
        var euXml = """
            <export>
              <sanctionEntity euReferenceNumber="EU.1042.88" logicalId="1042">
                <subjectType classificationCode="enterprise" />
                <regulation programme="SYR" />
                <nameAlias wholeName="Khorasan Trading Corp" />
                <nameAlias wholeName="Khorasan Exchange" />
                <citizenship countryIso2Code="SY" />
                <address city="Damascus" countryDescription="Syria" />
              </sanctionEntity>
            </export>
            """;

        using var euStream = new MemoryStream(Encoding.UTF8.GetBytes(euXml));
        var euResult = await SanctionsFeedParsers.ParseXmlStreamAsync("EU", BlacklistSourceDefaults.EuRefId, euStream);
        Assert.True(euResult.IsRootCompleted);
        Assert.Single(euResult.Records);
        Assert.Equal("EU-1042", euResult.Records[0].EntityExternalId);
        Assert.Equal("Khorasan Trading Corp", euResult.Records[0].FullName);
        Assert.Equal("CORPORATE", euResult.Records[0].EntityType);

        // 4. UK Sanctions XML
        var ukXml = """
            <Designations>
              <Designation>
                <UniqueID>RUS0091</UniqueID>
                <IndividualEntityShip>Individual</IndividualEntityShip>
                <RegimeName>Russia Sanctions Regime</RegimeName>
                <Names>
                  <Name><Name6>IVANOV</Name6><Name1>SERGEI</Name1><NameType>Primary Name</NameType></Name>
                  <Name><Name6>IVANOV</Name6><Name1>SERGEY</Name1><NameType>Alias</NameType></Name>
                </Names>
                <DOBs><DOB>01/02/1965</DOB></DOBs>
                <Nationalities><Nationality>Russian</Nationality></Nationalities>
              </Designation>
            </Designations>
            """;

        using var ukStream = new MemoryStream(Encoding.UTF8.GetBytes(ukXml));
        var ukResult = await SanctionsFeedParsers.ParseXmlStreamAsync("UK", BlacklistSourceDefaults.UkRefId, ukStream);
        Assert.True(ukResult.IsRootCompleted);
        Assert.Single(ukResult.Records);
        Assert.Equal("UK-RUS0091", ukResult.Records[0].EntityExternalId);
        Assert.Equal("SERGEI IVANOV", ukResult.Records[0].FullName);
    }

    [Fact]
    public void BlacklistRecordComparer_Ignores_Attribute_Order_And_Detects_Field_Changes_And_Reactivation()
    {
        var rec1 = new NormalizedBlacklistRecord
        {
            SourceType = "OFAC",
            SourceRefId = BlacklistSourceDefaults.OfacRefId,
            SourceList = "OFAC_SDN",
            EntityExternalId = "OFAC-100",
            FullName = "  Viktor   Bout ",
            EntityType = "INDIVIDUAL",
            Aliases = ["Alias B", "Alias A"],
            DatesOfBirth = ["1967-01-13"],
            Nationalities = ["RU", "UA"],
            Identifications = ["PASSPORT:123"],
            Addresses = ["Moscow, Russia"],
            Programs = ["SDGT"],
            Remarks = "Arms dealer"
        };

        // Reordered aliases and nationalities with extra whitespace
        var rec2Same = new NormalizedBlacklistRecord
        {
            SourceType = "OFAC",
            SourceRefId = BlacklistSourceDefaults.OfacRefId,
            SourceList = "OFAC_SDN",
            EntityExternalId = "OFAC-100",
            FullName = "VIKTOR BOUT",
            EntityType = "INDIVIDUAL",
            Aliases = ["alias a", "ALIAS B"],
            DatesOfBirth = ["1967-01-13"],
            Nationalities = ["ua", "RU"],
            Identifications = ["passport:123"],
            Addresses = ["moscow,   russia"],
            Programs = ["SDGT"],
            Remarks = "Arms   dealer"
        };

        var hash1 = BlacklistRecordComparer.ComputeContentHash(rec1);
        var hash2 = BlacklistRecordComparer.ComputeContentHash(rec2Same);
        Assert.Equal(hash1, hash2);

        // 1. Exact match -> IsExactMatch = true, ComparisonResult = EXACT_ATTRIBUTE_MATCH
        var skipResult = BlacklistRecordComparer.Compare(rec1, hash1, wasPreviouslyActive: true, rec2Same);
        Assert.True(skipResult.IsExactMatch);
        Assert.Equal("EXACT_ATTRIBUTE_MATCH", skipResult.ComparisonResult);

        // 2. Modified alias -> IsExactMatch = false, ChangedFields contains aliases
        var recModified = rec1 with
        {
            Aliases = ["Alias A", "Alias B", "New Alias C"]
        };

        var updateResult = BlacklistRecordComparer.Compare(rec1, hash1, wasPreviouslyActive: true, recModified);
        Assert.False(updateResult.IsExactMatch);
        Assert.StartsWith("CHANGED:", updateResult.ComparisonResult, StringComparison.Ordinal);

        // 3. Previously REMOVED record reappearing -> REACTIVATED_RECORD
        var reactivatedResult = BlacklistRecordComparer.Compare(rec1, hash1, wasPreviouslyActive: false, rec2Same);
        Assert.False(reactivatedResult.IsExactMatch);
        Assert.Equal("REACTIVATED_RECORD", reactivatedResult.ComparisonResult);
    }

    [Fact]
    public void BlacklistErrorTranslator_Maps_Technical_Failures_To_User_Friendly_Messages()
    {
        var timeoutMsg = BlacklistErrorTranslator.TranslateException("OFAC", new TaskCanceledException("Timed out"));
        Assert.Equal("NETWORK_TIMEOUT", timeoutMsg.ErrorCategory);
        Assert.Contains("Unable to connect to the OFAC data source", timeoutMsg.UserMessage, StringComparison.Ordinal);

        var http404Msg = BlacklistErrorTranslator.TranslateHttpStatus("UN", HttpStatusCode.NotFound, "Not Found");
        Assert.Equal("HTTP_404_NOT_FOUND", http404Msg.ErrorCategory);
        Assert.Contains("UN data file could not be found", http404Msg.UserMessage, StringComparison.Ordinal);

        var xmlMsg = BlacklistErrorTranslator.TranslateException("EU", new XmlException("Unexpected end of file"));
        Assert.Equal("INVALID_XML_FORMAT", xmlMsg.ErrorCategory);
        Assert.Contains("Existing EU records were not modified", xmlMsg.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BlacklistSyncService_Classifies_New_Updated_Skipped_Removed_And_Preserves_Active_On_Failure()
    {
        using var handler = new StubFeedHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        using var service = new BlacklistSyncService(
            connectionString: null,
            httpClient: httpClient,
            searchEngine: _searchEngine,
            inMemorySearchEngine: _searchEngine,
            logger: NullLogger<BlacklistSyncService>.Instance);

        // Verify schedule is initially disabled
        var schedule = await service.GetScheduleConfigAsync();
        Assert.False(schedule.IsScheduleEnabled);
        Assert.Equal("02:00", schedule.DailyExecutionTimeUtc);

        // Run 1: Initial manual sync for OFAC (2 records: 9812 and 9813)
        handler.SetResponse(BlacklistSourceDefaults.OfacDefaultUrl, HttpStatusCode.OK, """
            <sdnList>
              <sdnEntry><uid>9812</uid><firstName>Viktor</firstName><lastName>Bout</lastName><sdnType>Individual</sdnType></sdnEntry>
              <sdnEntry><uid>9813</uid><firstName>Ivan</firstName><lastName>Petrov</lastName><sdnType>Individual</sdnType></sdnEntry>
            </sdnList>
            """);

        var (accepted1, _, run1) = await service.TriggerSyncAsync("MANUAL", "admin", ["OFAC"], waitForCompletion: true);
        Assert.True(accepted1);
        Assert.NotNull(run1);
        Assert.Equal("SUCCESS", run1.Status);
        Assert.Equal(2, run1.TotalRetrieved);
        Assert.Equal(2, run1.TotalNew);
        Assert.Equal(0, run1.TotalUpdated);
        Assert.Equal(0, run1.TotalSkipped);
        Assert.Equal(0, run1.TotalRemoved);

        // Run 2: 9812 unchanged (SKIPPED), 9814 added (NEW), 9813 missing from latest feed (REMOVED)
        handler.SetResponse(BlacklistSourceDefaults.OfacDefaultUrl, HttpStatusCode.OK, """
            <sdnList>
              <sdnEntry><uid>9812</uid><firstName>Viktor</firstName><lastName>Bout</lastName><sdnType>Individual</sdnType></sdnEntry>
              <sdnEntry><uid>9814</uid><firstName>Sergei</firstName><lastName>Orlov</lastName><sdnType>Individual</sdnType></sdnEntry>
            </sdnList>
            """);

        var (_, _, run2) = await service.TriggerSyncAsync("MANUAL", "admin", ["OFAC"], waitForCompletion: true);
        Assert.NotNull(run2);
        Assert.Equal("SUCCESS", run2.Status);
        Assert.Equal(2, run2.TotalRetrieved);
        Assert.Equal(1, run2.TotalNew);      // 9814
        Assert.Equal(0, run2.TotalUpdated);
        Assert.Equal(1, run2.TotalSkipped);  // 9812
        Assert.Equal(1, run2.TotalRemoved);  // 9813 missing from latest complete feed

        // Verify skipped items are stored and retrievable via GetRunItemsAsync
        var skippedPage = await service.GetRunItemsAsync(run2.Id, action: "SKIPPED");
        Assert.Equal(1, skippedPage.TotalCount);
        Assert.Equal("OFAC-9812", skippedPage.Items[0].EntityExternalId);
        Assert.Equal("EXACT_ATTRIBUTE_MATCH", skippedPage.Items[0].ComparisonResult);

        // Run 3: 9813 reappears -> must be REACTIVATED and counted as UPDATED
        handler.SetResponse(BlacklistSourceDefaults.OfacDefaultUrl, HttpStatusCode.OK, """
            <sdnList>
              <sdnEntry><uid>9812</uid><firstName>Viktor</firstName><lastName>Bout</lastName><sdnType>Individual</sdnType></sdnEntry>
              <sdnEntry><uid>9813</uid><firstName>Ivan</firstName><lastName>Petrov</lastName><sdnType>Individual</sdnType></sdnEntry>
              <sdnEntry><uid>9814</uid><firstName>Sergei</firstName><lastName>Orlov</lastName><sdnType>Individual</sdnType></sdnEntry>
            </sdnList>
            """);

        var (_, _, run3) = await service.TriggerSyncAsync("MANUAL", "admin", ["OFAC"], waitForCompletion: true);
        Assert.NotNull(run3);
        Assert.Equal("SUCCESS", run3.Status);
        Assert.Equal(1, run3.TotalUpdated); // 9813 reactivated
        Assert.Equal(2, run3.TotalSkipped); // 9812 & 9814

        // Run 4: Simulate truncated/corrupted XML on OFAC -> must NOT mark any active records as REMOVED
        handler.SetResponse(BlacklistSourceDefaults.OfacDefaultUrl, HttpStatusCode.OK, "<sdnList><sdnEntry><uid>9812");
        var (_, _, run4) = await service.TriggerSyncAsync("MANUAL", "admin", ["OFAC"], waitForCompletion: true);
        Assert.NotNull(run4);
        Assert.Equal("FAILED", run4.Status);
        Assert.Equal(0, run4.TotalRemoved);

        var dist = await service.GetDistributionStatsAsync();
        var ofacDist = dist.Sources.First(s => s.SourceType == "OFAC");
        Assert.Equal(3, ofacDist.ActiveCount); // All 3 active records preserved!
    }

    [Fact]
    public async Task BulkIngestionPipeline_Detects_Duplicates_Across_More_Than_1000_Rows()
    {
        var parser = new AmlStreamingParser();
        var repo = new InMemoryBulkJobRepository();

        // Seed 1,250 existing watchlist records in repository
        var existingNames = new List<string>();
        for (int i = 1; i <= 1250; i++)
        {
            existingNames.Add(string.Create(CultureInfo.InvariantCulture, $"SANCTIONED PERSON {i:D4}"));
        }
        repo.SeedExistingEntities(existingNames);

        var notifier = new CallbackProgressNotifier(_ => { });
        var pipeline = new BulkIngestionPipeline(parser, _screeningEngine, repo, notifier);

        var jobId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await repo.CreateJobAsync(new BulkJobRecord
        {
            Id = jobId,
            UserId = userId,
            FileName = "large_1255_batch.csv",
            Status = "PENDING",
            KeepSkipped = true,
            SkippedFormat = "csv"
        });

        // Build CSV with all 1,250 existing records + 5 brand new records = 1,255 rows
        var sb = new StringBuilder();
        sb.AppendLine("AML_TYPE,AML_FULLNAME,AML_IDDETAILS,AML_CATEGORY,AML_DATEOFBIRTH,AML_REMARKS,AML_NATIONALITY,AML_PROGRAM,AML_ALIASNAME");
        for (int i = 1; i <= 1250; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"INDIVIDUAL,SANCTIONED PERSON {i:D4},ID-{i:D4},SANCTION,1975-01-01,Existing,RU,OFAC_SDN,");
        }
        for (int i = 1; i <= 5; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"INDIVIDUAL,BRAND NEW PERSON {i:D4},NEW-ID-{i:D4},CLEAN,1990-01-01,New,US,NONE,");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        var result = await pipeline.ProcessStreamAsync(
            jobId,
            TestTenantId,
            userId,
            "large_1255_batch.csv",
            "csv",
            stream,
            keepSkipped: true,
            skippedFormat: "csv");

        Assert.Equal(BulkJobStatus.Completed, result.Status);
        Assert.Equal(1255, result.TotalRows);
        Assert.Equal(1250, result.SkippedCount); // Every single duplicate past row 1,000 was detected and skipped!
        Assert.Equal(5, result.CleanCount);
    }

    private sealed class StubFeedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Content)> _responses = new(StringComparer.OrdinalIgnoreCase);

        public void SetResponse(string url, HttpStatusCode status, string content)
        {
            _responses[url] = (status, content);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (_responses.TryGetValue(url, out var configured))
            {
                return Task.FromResult(new HttpResponseMessage(configured.Status)
                {
                    Content = new StringContent(configured.Content, Encoding.UTF8, "application/xml")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Edith.Screening.Api.Contracts;
using Edith.Screening.Bulk.Models;
using Edith.Screening.Bulk.Repository;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Edith.Screening.Api.Tests;

public sealed class ScreeningApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ScreeningApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_Returns_Healthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task ScreenTransaction_MissingIdempotencyKey_Returns_BadRequest()
    {
        var request = new ScreenTransactionRequest
        {
            TransactionRefNo = "TX-ERR-001",
            Amount = 1000m,
            Currency = "AED",
            SenderName = "Ahmed Al-Mansoor",
            BeneficiaryName = "Fatima Al-Zahra"
        };

        // Note: No 'Idempotency-Key' header attached
        var response = await _client.PostAsJsonAsync("/api/v1/screen/transaction", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Missing Idempotency-Key", body);
    }

    [Fact]
    public async Task ScreenTransaction_CleanTransaction_Returns_Cleared()
    {
        var request = new ScreenTransactionRequest
        {
            TransactionRefNo = "TX-CLEAN-001",
            Amount = 50000m,
            Currency = "AED",
            SenderName = "Liam Alexander O'Connor",
            SenderNationality = "IE",
            SenderCountry = "IE",
            BeneficiaryName = "Sophia Isabella Miller",
            BeneficiaryCountry = "NZ"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ScreenTransactionResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("CLEARED", result.Status);
        Assert.False(result.IsHeld);
        Assert.False(result.IdempotencyReplayed);
        Assert.Null(result.CaseNumber);
    }

    [Fact]
    public async Task ScreenTransaction_SanctionHit_Returns_Held_With_CaseNumber()
    {
        var request = new ScreenTransactionRequest
        {
            TransactionRefNo = "TX-SANCTION-001",
            Amount = 500000m,
            Currency = "USD",
            SenderName = "Acme Global Trading FZE",
            BeneficiaryName = "Viktor Anatolyevich Bout",
            BeneficiaryCountry = "RU"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ScreenTransactionResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("HELD", result.Status);
        Assert.True(result.IsHeld);
        Assert.NotNull(result.CaseNumber);
        Assert.StartsWith("CASE-", result.CaseNumber);
        Assert.Equal("OFAC_SDN", result.TriggeredList);
        Assert.NotEmpty(result.Matches);
        Assert.Contains(result.Matches, m => m.MatchedEntityId == "OFAC-9812");

        var caseService = _factory.Services.GetRequiredService<Edith.Cases.Services.ICaseService>();
        var cases = await caseService.ListCasesAsync(Guid.Parse("01931000-0000-7000-8000-000000000000"));
        Assert.Contains(cases, c => c.CaseNumber == result.CaseNumber && c.Priority == Edith.Cases.Models.CasePriority.Critical);
    }

    [Fact]
    public async Task ScreenTransaction_SanctionedBank_Returns_Held()
    {
        var request = new ScreenTransactionRequest
        {
            TransactionRefNo = "TX-BANK-001",
            Amount = 250000m,
            Currency = "USD",
            SenderName = "Dubai Trading LLC",
            BeneficiaryName = "European Tech Corp",
            IntermediaryBankName = "Bank of Baghdad"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ScreenTransactionResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("HELD", result.Status);
        Assert.True(result.IsHeld);
        Assert.Contains(result.Matches, m => m.Role == "IntermediaryBank" && m.MatchedEntityId == "OFAC-BANK-0099");
    }

    [Fact]
    public async Task ScreenTransaction_ActiveWhitelist_Suppresses_Hold()
    {
        var tenantId = Guid.NewGuid();

        // 1. Add whitelist entry for Viktor Bout's entity ID (OFAC-9812)
        var whitelistReq = new CreateWhitelistRequest
        {
            TenantId = tenantId,
            EntityIdentifier = "OFAC-9812",
            Reason = "Diplomatic exemption granted under ministerial decree",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };

        var wlResponse = await _client.PostAsJsonAsync("/api/v1/whitelists", whitelistReq);
        Assert.Equal(HttpStatusCode.Created, wlResponse.StatusCode);

        // 2. Screen the sanctioned transaction for this tenant
        var screenReq = new ScreenTransactionRequest
        {
            TenantId = tenantId,
            TransactionRefNo = "TX-WHITELIST-001",
            Amount = 150000m,
            Currency = "USD",
            SenderName = "Exempt Commercial Entity",
            BeneficiaryName = "Viktor Anatolyevich Bout"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(screenReq)
        };
        httpRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        httpRequest.Headers.Add("X-Tenant-ID", tenantId.ToString());

        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ScreenTransactionResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("CLEARED", result.Status);
        Assert.False(result.IsHeld);
        Assert.True(result.IsWhitelisted);
        Assert.Contains("CLEARED_BY_WHITELIST", result.HoldReason);
    }

    [Fact]
    public async Task ScreenTransaction_IdempotencyReplay_Returns_Cached_Result()
    {
        var idempotencyKey = $"IDEMP-{Guid.NewGuid()}";
        var tenantId = Guid.NewGuid();

        var request = new ScreenTransactionRequest
        {
            TenantId = tenantId,
            TransactionRefNo = "TX-IDEMP-001",
            Amount = 75000m,
            Currency = "AED",
            SenderName = "Salim Al-Nuaimi",
            BeneficiaryName = "Fatima Al-Kaabi"
        };

        // 1. First execution
        using var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(request)
        };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        request1.Headers.Add("X-Tenant-ID", tenantId.ToString());

        var response1 = await _client.SendAsync(request1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var result1 = await response1.Content.ReadFromJsonAsync<ScreenTransactionResponse>(JsonOptions);
        Assert.NotNull(result1);
        Assert.False(result1.IdempotencyReplayed);

        // 2. Second execution (Replay with same idempotency key)
        using var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(request)
        };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        request2.Headers.Add("X-Tenant-ID", tenantId.ToString());

        var response2 = await _client.SendAsync(request2);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
        var result2 = await response2.Content.ReadFromJsonAsync<ScreenTransactionResponse>(JsonOptions);
        Assert.NotNull(result2);
        Assert.True(result2.IdempotencyReplayed);
        Assert.Equal(result1.OverallScore, result2.OverallScore);
        Assert.Equal(result1.Status, result2.Status);
    }

    [Fact]
    public async Task WatchlistSync_DynamicallyIndexesNewEntity()
    {
        var syncReq = new WatchlistSyncRequest
        {
            Entities = new[]
            {
                new WatchlistEntityDto
                {
                    EntityExternalId = "DYNAMIC-999",
                    EntityType = "INDIVIDUAL",
                    FullName = "CARLOS SANCHEZ ILICH",
                    Nationality = "VE",
                    SourceList = "INTERPOL_RED",
                    Category = "SANCTION",
                    IsBlocked = true,
                    IsActive = true
                }
            }
        };

        var syncRes = await _client.PostAsJsonAsync("/api/v1/watchlist/sync", syncReq);
        Assert.Equal(HttpStatusCode.OK, syncRes.StatusCode);

        // Screen against newly synced entity
        var screenReq = new ScreenTransactionRequest
        {
            TransactionRefNo = "TX-SYNC-001",
            Amount = 88000m,
            Currency = "EUR",
            SenderName = "European Logistics",
            BeneficiaryName = "Carlos Sanchez Ilich"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(screenReq)
        };
        httpRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(httpRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ScreenTransactionResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("HELD", result.Status);
        Assert.True(result.IsHeld);
        Assert.Contains(result.Matches, m => m.MatchedEntityId == "DYNAMIC-999");
    }

    [Fact]
    public async Task BulkUpload_CsvFile_Returns_202Accepted_And_CreatesJob()
    {
        var csvContent = """
        AML_TYPE,AML_FULLNAME,AML_IDDETAILS,AML_CATEGORY,AML_DATEOFBIRTH,AML_REMARKS,AML_NATIONALITY,AML_PROGRAM,AML_ALIASNAME
        INDIVIDUAL,VIKTOR ANATOLYEVICH BOUT,PASSPORT-RU-882190,SANCTION,1967-01-13,Arms,RU,OFAC_SDN,Victor Bout
        INDIVIDUAL,AHMED AL-MANSOOR,PASSPORT-AE-102938,CLEAN,1985-02-10,Commercial,AE,NONE,
        """;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bulk/upload")
        {
            Content = new StringContent(csvContent, Encoding.UTF8, "text/csv")
        };
        request.Headers.Add("X-File-Name", "batch_test.csv");
        request.Headers.Add("X-Tenant-ID", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BulkUploadAcceptedResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.JobId);
        Assert.Equal("batch_test.csv", result.FileName);
        Assert.Equal("CSV", result.Format);
        Assert.Equal("ACCEPTED", result.Status);
        Assert.Contains($"/api/v1/bulk/jobs/{result.JobId}", result.ProgressUrl);

        // Fetch job status
        var statusRes = await _client.GetAsync($"/api/v1/bulk/jobs/{result.JobId}");
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
        var status = await statusRes.Content.ReadFromJsonAsync<BulkJobStatusResponse>(JsonOptions);
        Assert.NotNull(status);
        Assert.Equal(result.JobId, status.JobId);
    }

    [Fact]
    public async Task BulkUpload_JsonFile_Returns_202Accepted()
    {
        var jsonContent = """
        [
          {
            "AML_TYPE": "INDIVIDUAL",
            "AML_FULLNAME": "VIKTOR ANATOLYEVICH BOUT",
            "AML_CATEGORY": "SANCTION"
          }
        ]
        """;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bulk/upload")
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-File-Name", "sanctions.json");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BulkUploadAcceptedResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("JSON", result.Format);
        Assert.Equal("ACCEPTED", result.Status);
    }

    [Fact]
    public async Task BulkUpload_UnsupportedFormat_Returns_400BadRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bulk/upload")
        {
            Content = new StringContent("some text content", Encoding.UTF8, "text/plain")
        };
        request.Headers.Add("X-File-Name", "unsupported.pdf");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpload_WithKeepSkipped_And_DownloadSkippedEndpoint()
    {
        var csvContent = """
        AML_TYPE,AML_FULLNAME,AML_IDDETAILS,AML_CATEGORY,AML_DATEOFBIRTH,AML_REMARKS,AML_NATIONALITY,AML_PROGRAM,AML_ALIASNAME
        INDIVIDUAL,VIKTOR ANATOLYEVICH BOUT,PASSPORT-RU-882190,SANCTION,1967-01-13,Arms,RU,OFAC_SDN,Victor Bout
        """;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/bulk/upload?keepSkipped=true&skippedFormat=json")
        {
            Content = new StringContent(csvContent, Encoding.UTF8, "text/csv")
        };
        request.Headers.Add("X-File-Name", "test_skipped.csv");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<BulkUploadAcceptedResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result.KeepSkipped);
        Assert.Equal("JSON", result.SkippedFormat);
        Assert.NotNull(result.SkippedDownloadUrl);
        Assert.Contains($"/api/v1/bulk/jobs/{result.JobId}/skipped", result.SkippedDownloadUrl);

        // Fetch job status
        var statusRes = await _client.GetAsync($"/api/v1/bulk/jobs/{result.JobId}");
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
        var status = await statusRes.Content.ReadFromJsonAsync<BulkJobStatusResponse>(JsonOptions);
        Assert.NotNull(status);
        Assert.Equal(result.JobId, status.JobId);
    }

    [Fact]
    public async Task DownloadSkippedRecords_Returns_File_With_Requested_Format()
    {
        var repo = _factory.Services.GetRequiredService<IBulkJobRepository>();
        var jobId = Guid.NewGuid();
        var userId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        await repo.CreateJobAsync(new BulkJobRecord
        {
            Id = jobId,
            UserId = userId,
            FileName = "skipped_test.csv",
            TotalRecords = 1,
            ProcessedRecords = 1,
            SkippedRecords = 1,
            Status = "COMPLETED",
            KeepSkipped = true,
            SkippedFormat = "csv"
        });

        await repo.SaveSkippedRecordsAsync(jobId, [
            new AmlEntityRecord
            {
                AmlType = "INDIVIDUAL",
                AmlFullName = "VIKTOR BOUT",
                AmlNationality = "RU",
                AmlCategory = "SANCTION"
            }
        ]);

        // 1. Download default (csv)
        var csvResponse = await _client.GetAsync($"/api/v1/bulk/jobs/{jobId}/skipped");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.Equal("text/csv", csvResponse.Content.Headers.ContentType?.MediaType);
        var csvContent = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("VIKTOR BOUT", csvContent);
        Assert.Contains("AML_FULLNAME", csvContent);

        // 2. Download with format query parameter override (json)
        var jsonResponse = await _client.GetAsync($"/api/v1/bulk/jobs/{jobId}/skipped?format=json");
        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        Assert.Equal("application/json", jsonResponse.Content.Headers.ContentType?.MediaType);
        var jsonContent = await jsonResponse.Content.ReadAsStringAsync();
        Assert.Contains("VIKTOR BOUT", jsonContent);

        // 3. Download with format query parameter override (xml)
        var xmlResponse = await _client.GetAsync($"/api/v1/bulk/jobs/{jobId}/skipped?format=xml");
        Assert.Equal(HttpStatusCode.OK, xmlResponse.StatusCode);
        Assert.Equal("application/xml", xmlResponse.Content.Headers.ContentType?.MediaType);
        var xmlContent = await xmlResponse.Content.ReadAsStringAsync();
        Assert.Contains("<AML_FULLNAME>VIKTOR BOUT</AML_FULLNAME>", xmlContent);
    }

    [Fact]
    public async Task WatchlistAndWhitelistGovernance_CrudAndSoftRevocation_Works()
    {
        // 1. Sync a new INTERNAL_BLACKLIST entity with AliasName and IdDetails
        var externalId = $"BLK-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var syncReq = new WatchlistSyncRequest
        {
            Entities = new[]
            {
                new WatchlistEntityDto
                {
                    EntityExternalId = externalId,
                    EntityType = "CORPORATE",
                    FullName = "KHORRAMSHAHR MARITIME LOGISTICS",
                    AliasName = "KML SHIPPING FZE",
                    IdDetails = "IMO-9988771 | CBUAE Directive 2026/09",
                    Nationality = "IR",
                    SourceList = "INTERNAL_BLACKLIST",
                    Category = "SANCTION",
                    IsBlocked = true,
                    IsActive = true
                }
            }
        };

        var syncRes = await _client.PostAsJsonAsync("/api/v1/watchlist/sync", syncReq);
        Assert.Equal(HttpStatusCode.OK, syncRes.StatusCode);
        var syncJson = await syncRes.Content.ReadAsStringAsync();
        Assert.Contains("wormHash", syncJson);

        // 2. Verify GET /api/v1/watchlist returns the entity with AliasName & IdDetails
        var listRes = await _client.GetAsync($"/api/v1/watchlist?id={externalId}");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var listBody = await listRes.Content.ReadAsStringAsync();
        Assert.Contains("KHORRAMSHAHR MARITIME LOGISTICS", listBody);
        Assert.Contains("KML SHIPPING FZE", listBody);
        Assert.Contains("IMO-9988771", listBody);

        // 3. Deactivate the watchlist entity (IsActive = false) and confirm screening no longer blocks it
        var deactivateReq = new WatchlistSyncRequest
        {
            Entities = new[]
            {
                new WatchlistEntityDto
                {
                    EntityExternalId = externalId,
                    EntityType = "CORPORATE",
                    FullName = "KHORRAMSHAHR MARITIME LOGISTICS",
                    AliasName = "KML SHIPPING FZE",
                    IdDetails = "IMO-9988771 | DEACTIVATED: CBUAE Lift",
                    Nationality = "IR",
                    SourceList = "INTERNAL_BLACKLIST",
                    Category = "SANCTION",
                    IsBlocked = false,
                    IsActive = false
                }
            }
        };
        var deactivateRes = await _client.PostAsJsonAsync("/api/v1/watchlist/sync", deactivateReq);
        Assert.Equal(HttpStatusCode.OK, deactivateRes.StatusCode);

        using var txReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(new ScreenTransactionRequest
            {
                TransactionRefNo = "TX-DEACT-001",
                Amount = 15000m,
                Currency = "AED",
                SenderName = "Dubai Port Trading",
                BeneficiaryName = "Khorramshahr Maritime Logistics"
            })
        };
        txReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var txRes = await _client.SendAsync(txReq);
        var txResult = await txRes.Content.ReadFromJsonAsync<ScreenTransactionResponse>(JsonOptions);
        Assert.NotNull(txResult);
        Assert.DoesNotContain(txResult.Matches, m => m.MatchedEntityId == externalId);

        // 4. Create and soft-revoke a whitelist entry, confirming includeInactive=true preserves it for audit
        var tenantId = Guid.NewGuid();
        var createWlRes = await _client.PostAsJsonAsync("/api/v1/whitelists", new CreateWhitelistRequest
        {
            TenantId = tenantId,
            EntityIdentifier = "OFAC-9812",
            Reason = "Temporary clearance",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(90)
        });
        Assert.Equal(HttpStatusCode.Created, createWlRes.StatusCode);
        using var createdDoc = JsonDocument.Parse(await createWlRes.Content.ReadAsStringAsync());
        var wlId = createdDoc.RootElement.GetProperty("id").GetGuid();

        var delRes = await _client.DeleteAsync($"/api/v1/whitelists/{wlId}?tenantId={tenantId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        var activeOnlyRes = await _client.GetAsync($"/api/v1/whitelists?tenantId={tenantId}&includeInactive=false");
        var activeOnlyBody = await activeOnlyRes.Content.ReadAsStringAsync();
        Assert.DoesNotContain(wlId.ToString(), activeOnlyBody);

        var allGovernanceRes = await _client.GetAsync($"/api/v1/whitelists?tenantId={tenantId}&includeInactive=true");
        var allGovernanceBody = await allGovernanceRes.Content.ReadAsStringAsync();
        Assert.Contains(wlId.ToString(), allGovernanceBody);
    }

    [Fact]
    public async Task NameSearch_AdvancedSearchModes_Work()
    {
        // 1. EXACT NAME mode: literal normalized exact match
        var exactReq = new NameSearchRequest
        {
            Name = "VIKTOR ANATOLYEVICH BOUT",
            SearchMode = "EXACT NAME"
        };
        var exactRes = await _client.PostAsJsonAsync("/api/v1/screen/name-search", exactReq);
        Assert.Equal(HttpStatusCode.OK, exactRes.StatusCode);
        var exactBody = await exactRes.Content.ReadFromJsonAsync<NameSearchResponse>(JsonOptions);
        Assert.NotNull(exactBody);
        Assert.NotEmpty(exactBody.Matches);
        Assert.Contains(exactBody.Matches, m => m.ExternalId == "OFAC-9812");

        // 2. ID + NAME mode: requires both or exact ID
        var idAndNameReq = new NameSearchRequest
        {
            Name = "Viktor Bout",
            IdNumber = "OFAC-9812",
            SearchMode = "ID + NAME"
        };
        var idNameRes = await _client.PostAsJsonAsync("/api/v1/screen/name-search", idAndNameReq);
        Assert.Equal(HttpStatusCode.OK, idNameRes.StatusCode);
        var idNameBody = await idNameRes.Content.ReadFromJsonAsync<NameSearchResponse>(JsonOptions);
        Assert.NotNull(idNameBody);
        Assert.NotEmpty(idNameBody.Matches);
        Assert.Equal("OFAC-9812", idNameBody.Matches[0].ExternalId);

        // 3. PHONETIC NAME MATCH mode: matches sound-alike pronunciation via Double Metaphone
        var phoneticReq = new NameSearchRequest
        {
            Name = "Victor Bout",
            SearchMode = "PHONETIC NAME MATCH",
            MinScore = 70m
        };
        var phoneticRes = await _client.PostAsJsonAsync("/api/v1/screen/name-search", phoneticReq);
        Assert.Equal(HttpStatusCode.OK, phoneticRes.StatusCode);
        var phoneticBody = await phoneticRes.Content.ReadFromJsonAsync<NameSearchResponse>(JsonOptions);
        Assert.NotNull(phoneticBody);
        Assert.NotEmpty(phoneticBody.Matches);
        Assert.Contains(phoneticBody.Matches, m => m.ExternalId == "OFAC-9812");
    }
}

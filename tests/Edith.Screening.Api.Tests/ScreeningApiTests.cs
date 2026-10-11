using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Edith.Screening.Api.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Edith.Screening.Api.Tests;

public sealed class ScreeningApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ScreeningApiTests(WebApplicationFactory<Program> factory)
    {
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
            SenderName = "Ahmed Mohammed Al-Fassi",
            SenderNationality = "AE",
            SenderCountry = "AE",
            BeneficiaryName = "Rashid Saeed Al-Nuaimi",
            BeneficiaryCountry = "AE"
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
}

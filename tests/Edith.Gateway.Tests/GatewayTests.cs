using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Edith.Gateway.Models;
using Edith.Gateway.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Edith.Gateway.Tests;

public sealed class GatewayTests : IClassFixture<WebApplicationFactory<Edith.Gateway.Program>>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GatewayTests(WebApplicationFactory<Edith.Gateway.Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_Returns_Healthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Edith.Gateway", content);
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task Unauthenticated_Requests_To_Entitlements_Are_Rejected()
    {
        // Calling /api/v1/iam/entitlements without Bearer token
        var response = await _client.GetAsync("/api/v1/iam/entitlements");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenEndpoint_Issues_Valid_Jwt_Token()
    {
        var request = new TokenRequest
        {
            Username = "tariq.mlro",
            Password = "Password123!",
            Role = EdithRoles.Mlro,
            TenantId = InMemoryTenantRegistry.DefaultTenantId
        };

        var response = await _client.PostAsJsonAsync("/api/v1/iam/token", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokenResult = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(tokenResult);
        Assert.Equal("Bearer", tokenResult.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(tokenResult.AccessToken));
        Assert.Equal(EdithRoles.Mlro, tokenResult.Role);
        Assert.Equal(InMemoryTenantRegistry.DefaultTenantId, tokenResult.TenantId);
    }

    [Fact]
    public async Task Entitlements_Returns_Role_Specific_Menus_For_Mlro()
    {
        // 1. Obtain token as MLRO
        var tokenReq = new TokenRequest
        {
            Username = "fatima.mlro",
            Password = "Password123!",
            Role = EdithRoles.Mlro,
            TenantId = InMemoryTenantRegistry.DefaultTenantId
        };
        var tokenRes = await _client.PostAsJsonAsync("/api/v1/iam/token", tokenReq);
        var token = await tokenRes.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(token);

        // 2. Fetch entitlements
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/iam/entitlements");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entitlements = await response.Content.ReadFromJsonAsync<UserEntitlementsResponse>(JsonOptions);
        Assert.NotNull(entitlements);
        Assert.Equal(EdithRoles.Mlro, entitlements.Role);
        Assert.Equal(InMemoryTenantRegistry.DefaultTenantId, entitlements.TenantId);

        // MLRO must have case disposal, freeze, and STR export actions
        var caseMenu = entitlements.Menus.FirstOrDefault(m => m.Key == "cases");
        Assert.NotNull(caseMenu);
        Assert.Contains("dispose", caseMenu.AllowedActions);
        Assert.Contains("freeze", caseMenu.AllowedActions);
        Assert.Contains("str_export", caseMenu.AllowedActions);

        // MLRO should NOT have system masters/admin menu
        Assert.DoesNotContain(entitlements.Menus, m => m.Key == "masters");
    }

    [Fact]
    public async Task Entitlements_Returns_Restricted_Menus_For_Analyst()
    {
        // 1. Obtain token as ANALYST
        var tokenReq = new TokenRequest
        {
            Username = "khalid.analyst",
            Password = "Password123!",
            Role = EdithRoles.Analyst,
            TenantId = InMemoryTenantRegistry.DefaultTenantId
        };
        var tokenRes = await _client.PostAsJsonAsync("/api/v1/iam/token", tokenReq);
        var token = await tokenRes.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(token);

        // 2. Fetch entitlements
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/iam/entitlements");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entitlements = await response.Content.ReadFromJsonAsync<UserEntitlementsResponse>(JsonOptions);
        Assert.NotNull(entitlements);
        Assert.Equal(EdithRoles.Analyst, entitlements.Role);

        // Analyst must NOT have dispose or freeze permissions
        var caseMenu = entitlements.Menus.FirstOrDefault(m => m.Key == "cases");
        Assert.NotNull(caseMenu);
        Assert.DoesNotContain("dispose", caseMenu.AllowedActions);
        Assert.DoesNotContain("freeze", caseMenu.AllowedActions);
        Assert.Contains("recommend", caseMenu.AllowedActions);

        // Analyst must NOT have rules builder or masters menu
        Assert.DoesNotContain(entitlements.Menus, m => m.Key == "rules");
        Assert.DoesNotContain(entitlements.Menus, m => m.Key == "masters");
    }

    [Fact]
    public async Task Tenant_Isolation_Rejects_Unregistered_Tenant()
    {
        var unknownTenantId = Guid.NewGuid();

        // 1. Request with token forged or pointing to an unindexed tenant
        var tokenReq = new TokenRequest
        {
            Username = "rogue.user",
            Password = "Password123!",
            Role = EdithRoles.Analyst,
            TenantId = InMemoryTenantRegistry.DefaultTenantId
        };
        var tokenRes = await _client.PostAsJsonAsync("/api/v1/iam/token", tokenReq);
        var token = await tokenRes.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions);
        Assert.NotNull(token);

        // 2. Override tenant header with unknown tenant
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/iam/entitlements");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        request.Headers.Add("X-Tenant-ID", unknownTenantId.ToString());

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Tenant Access Denied", body);
    }

    [Fact]
    public async Task Case_Investigation_Workflow_EndToEnd_From_Hold_To_Resolution()
    {
        // 1. Screen blacklisted transaction (Viktor Anatolyevich Bout)
        var screenReq = new
        {
            transactionRefNo = "TX-CASE-E2E-001",
            amount = 750000m,
            currency = "USD",
            senderName = "Al-Hilal Trading LLC",
            beneficiaryName = "Viktor Anatolyevich Bout",
            beneficiaryCountry = "RU"
        };

        using var screenHttp = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(screenReq)
        };
        screenHttp.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        screenHttp.Headers.Add("X-Tenant-ID", InMemoryTenantRegistry.DefaultTenantId.ToString());
        var screenRes = await _client.SendAsync(screenHttp);
        Assert.Equal(HttpStatusCode.OK, screenRes.StatusCode);

        // 2. Analyst views cases list
        var casesRes = await _client.GetAsync("/api/v1/cases");
        Assert.Equal(HttpStatusCode.OK, casesRes.StatusCode);
        var casesJson = await casesRes.Content.ReadAsStringAsync();
        using var casesDoc = JsonDocument.Parse(casesJson);
        var caseItem = casesDoc.RootElement.EnumerateArray().First(c =>
            c.GetProperty("primarySubjectName").GetString() == "Viktor Anatolyevich Bout" &&
            c.GetProperty("status").GetString() == "Open");
        var caseId = Guid.Parse(caseItem.GetProperty("id").GetString()!);

        // 3. Analyst acquires pessimistic 15-minute lease lock
        var lockRes = await _client.PostAsync($"/api/v1/cases/{caseId}/acquire-lock", null);
        Assert.Equal(HttpStatusCode.OK, lockRes.StatusCode);

        // 4. Analyst adds multi-type comment
        var noteReq = new { content = "Verified OFAC SDN hit on Viktor Bout. Escalating to MLRO for freeze.", noteType = "INVESTIGATION" };
        var noteRes = await _client.PostAsJsonAsync($"/api/v1/cases/{caseId}/comments", noteReq);
        Assert.Equal(HttpStatusCode.OK, noteRes.StatusCode);

        // 5. Transition to UnderReview then PendingApproval
        var transReview = new { newStatus = "UnderReview", userRole = "ANALYST", reason = "Analyst started EDD" };
        var reviewRes = await _client.PostAsJsonAsync($"/api/v1/cases/{caseId}/transition", transReview);
        Assert.Equal(HttpStatusCode.OK, reviewRes.StatusCode);

        var transApprove = new { newStatus = "PendingApproval", userRole = "ANALYST", reason = "Four-eyes escalation to MLRO" };
        var approveRes = await _client.PostAsJsonAsync($"/api/v1/cases/{caseId}/transition", transApprove);
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        // 6. MLRO resolves case with ReportedStrFreeze
        var resolveReq = new { resolution = "ReportedStrFreeze", rationale = "True match confirmed. Funds frozen under UAE UNSC resolutions." };
        var resolveRes = await _client.PostAsJsonAsync($"/api/v1/cases/{caseId}/resolve", resolveReq);
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);

        // 7. Verify case detail reflects Resolved state, audit entries, and added comments
        var detailRes = await _client.GetAsync($"/api/v1/cases/{caseId}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        var detailJson = await detailRes.Content.ReadAsStringAsync();
        using var detailDoc = JsonDocument.Parse(detailJson);
        Assert.Equal("Resolved", detailDoc.RootElement.GetProperty("status").GetString());
        Assert.True(detailDoc.RootElement.GetProperty("comments").GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Portal_BulkUpload_And_Direct_CaseResolution_Succeed_Via_Gateway()
    {
        // 1. Upload CSV via Gateway /api/v1/bulk/upload with keepSkipped=true
        var csv = "AML_TYPE,AML_FULLNAME,AML_IDDETAILS,AML_CATEGORY,AML_NATIONALITY\nINDIVIDUAL,VIKTOR ANATOLYEVICH BOUT,OFAC-9812,SANCTION,RU\nINDIVIDUAL,SALEM AL-MANSOORI,ID-9991,CLEAN,AE\n";
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "file", "portal_test.csv");

        var uploadRes = await _client.PostAsync("/api/v1/bulk/upload?keepSkipped=true&skippedFormat=json", form);
        Assert.Equal(HttpStatusCode.Accepted, uploadRes.StatusCode);

        // 2. Screen a distinct hit to create an Open case and resolve it directly using UI string 'FalsePositive'
        using var screenHttp = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(new
            {
                transactionRefNo = "TX-PORTAL-UI-002",
                amount = 120000m,
                currency = "AED",
                senderName = "Gulf Traders",
                beneficiaryName = "Tariq Aziz",
                beneficiaryCountry = "IQ",
                minThresholdScore = 60m
            })
        };
        screenHttp.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        screenHttp.Headers.Add("X-Tenant-ID", InMemoryTenantRegistry.DemoTenantId.ToString());
        var screenRes = await _client.SendAsync(screenHttp);
        Assert.Equal(HttpStatusCode.OK, screenRes.StatusCode);

        var casesRes = await _client.GetAsync("/api/v1/cases");
        var casesJson = await casesRes.Content.ReadAsStringAsync();
        using var casesDoc = JsonDocument.Parse(casesJson);
        var openCase = casesDoc.RootElement.EnumerateArray().First(c =>
            c.GetProperty("primarySubjectName").GetString() == "Tariq Aziz" &&
            c.GetProperty("status").GetString() == "Open");

        var openCaseId = openCase.GetProperty("id").GetString();
        var resolveRes = await _client.PostAsJsonAsync($"/api/v1/cases/{openCaseId}/resolve", new
        {
            resolution = "FalsePositive",
            rationale = "Cleared via UI portal MLRO sign-off"
        });
        Assert.Equal(HttpStatusCode.OK, resolveRes.StatusCode);
    }
}

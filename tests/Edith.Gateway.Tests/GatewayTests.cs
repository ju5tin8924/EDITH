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
}

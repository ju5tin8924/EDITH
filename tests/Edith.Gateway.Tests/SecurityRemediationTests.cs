using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Edith.Gateway.Models;
using Edith.Gateway.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Edith.Gateway.Tests;

public sealed class SecurityRemediationTests : IClassFixture<WebApplicationFactory<Edith.Gateway.Program>>
{
    private readonly WebApplicationFactory<Edith.Gateway.Program> _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SecurityRemediationTests(WebApplicationFactory<Edith.Gateway.Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private string GenerateToken(string role, Guid? tenantId = null, TimeSpan? lifetime = null)
    {
        var tid = tenantId ?? InMemoryTenantRegistry.DefaultTenantId;
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var user = role switch
        {
            EdithRoles.SuperAdmin => "superadmin@edith.aml.cloud",
            EdithRoles.Admin => "admin@edith.aml.cloud",
            EdithRoles.Mlro => "istr.mlro@adcb.ae",
            _ => "khalid.analyst"
        };
        return tokenService.GenerateToken(user, role, tid, lifetime).AccessToken;
    }

    private HttpClient CreateAuthenticatedClient(string role, Guid? tenantId = null, TimeSpan? lifetime = null)
    {
        var tid = tenantId ?? InMemoryTenantRegistry.DefaultTenantId;
        var token = GenerateToken(role, tid, lifetime);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-ID", tid.ToString());
        return client;
    }

    [Fact]
    public async Task Req01_AnonymousUsers_CannotAccessProtectedEndpoints_ReturnsUnauthorized()
    {
        // 1. SEC-01 verification: Protected endpoints must reject unauthenticated requests with 401
        var endpoints = new[]
        {
            (HttpMethod.Get, "/api/v1/cases"),
            (HttpMethod.Get, "/api/v1/rules"),
            (HttpMethod.Get, "/api/v1/settings/masters"),
            (HttpMethod.Get, "/api/v1/iam/users"),
            (HttpMethod.Get, "/api/v1/kyc"),
            (HttpMethod.Get, "/api/v1/notifications/rules"),
            (HttpMethod.Get, "/api/v1/reports/audit"),
            (HttpMethod.Get, "/api/v1/delta/sources")
        };

        foreach (var (method, path) in endpoints)
        {
            using var req = new HttpRequestMessage(method, path);
            var res = await _client.SendAsync(req);
            Assert.True(res.StatusCode == HttpStatusCode.Unauthorized,
                $"Expected 401 Unauthorized for anonymous request to {path}, got {res.StatusCode}");
        }
    }

    [Fact]
    public async Task Req02_RegularUsers_CannotAccessAdministratorEndpoints_ReturnsForbidden()
    {
        // 2. Regular Analyst cannot perform administrative operations or MLRO actions
        var analystClient = CreateAuthenticatedClient(EdithRoles.Analyst);

        var adminEndpoints = new[]
        {
            (HttpMethod.Post, "/api/v1/iam/users", (object)new CreateIamUserRequest("test@test.com", "Secret#123", "Test User", "ANALYST", null, "Desk", "ON_DUTY")),
            (HttpMethod.Post, "/api/v1/iam/groups", (object)new CreateIamGroupRequest("TEST_GROUP")),
            (HttpMethod.Get, "/api/v1/settings/masters", (object?)null),
            (HttpMethod.Get, "/api/v1/notifications/rules", (object?)null),
            (HttpMethod.Post, "/api/v1/rules", (object)new { ruleCode = "TEST_R", ruleName = "Test", ruleType = "TRANSACTION_THRESHOLD", severity = "HIGH" }),
            (HttpMethod.Post, $"/api/v1/cases/{Guid.NewGuid()}/resolve", (object)new { resolution = "ClearedFalsePositive", rationale = "Analyst unauthorized resolve attempt" })
        };

        foreach (var (method, path, body) in adminEndpoints)
        {
            using var req = new HttpRequestMessage(method, path);
            if (body != null)
            {
                req.Content = JsonContent.Create(body);
            }

            var res = await analystClient.SendAsync(req);
            Assert.True(res.StatusCode == HttpStatusCode.Forbidden,
                $"Expected 403 Forbidden for Analyst accessing admin/MLRO endpoint {path}, got {res.StatusCode}");
        }
    }

    [Fact]
    public async Task Req03_UsersCannotImpersonateOtherUsers_AuditPreservesAuthenticatedActor()
    {
        // 3. SEC-02 verification: Case comments and transitions obtain identity strictly from authenticated JWT
        var mlroClient = CreateAuthenticatedClient(EdithRoles.Mlro);

        // Create an open case via transaction screening
        var screenReq = new
        {
            transactionRefNo = $"TX-IMPERSONATE-{Guid.NewGuid():N}",
            amount = 500000m,
            currency = "USD",
            senderName = "Al-Hilal Trading LLC",
            beneficiaryName = "Viktor Anatolyevich Bout",
            beneficiaryCountry = "RU"
        };
        using var screenHttp1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(screenReq)
        };
        screenHttp1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var screenRes = await mlroClient.SendAsync(screenHttp1);
        Assert.Equal(HttpStatusCode.OK, screenRes.StatusCode);

        // Fetch case ID
        var casesRes = await mlroClient.GetAsync("/api/v1/cases");
        var casesJson = await casesRes.Content.ReadAsStringAsync();
        using var casesDoc = JsonDocument.Parse(casesJson);
        var targetCase = casesDoc.RootElement.EnumerateArray()
            .First(c => c.GetProperty("primarySubjectName").GetString() == "Viktor Anatolyevich Bout" && c.GetProperty("status").GetString() == "Open");
        var caseId = Guid.Parse(targetCase.GetProperty("id").GetString()!);

        // Attempt to post comment anonymously -> Must fail 401 (proves hardcoded fallback is eliminated)
        var anonNoteReq = new { content = "Attempting spoofed unauthenticated comment", noteType = "INVESTIGATION" };
        var anonRes = await _client.PostAsJsonAsync($"/api/v1/cases/{caseId}/comments", anonNoteReq);
        Assert.Equal(HttpStatusCode.Unauthorized, anonRes.StatusCode);

        // Post comment with authenticated MLRO client
        var authNoteReq = new { content = "Legitimate authenticated MLRO investigation review.", noteType = "INVESTIGATION" };
        var authRes = await mlroClient.PostAsJsonAsync($"/api/v1/cases/{caseId}/comments", authNoteReq);
        Assert.Equal(HttpStatusCode.OK, authRes.StatusCode);

        // Verify case detail records the authenticated actor, not any spoofed identity
        var detailRes = await mlroClient.GetAsync($"/api/v1/cases/{caseId}");
        var detailJson = await detailRes.Content.ReadAsStringAsync();
        using var detailDoc = JsonDocument.Parse(detailJson);
        var comments = detailDoc.RootElement.GetProperty("comments");
        var lastComment = comments.EnumerateArray().Last();
        Assert.Equal("istr.mlro@adcb.ae", lastComment.GetProperty("authorName").GetString());
    }

    [Fact]
    public async Task Req04_CrossTenantIsolation_UsersCannotAccessOtherTenantRecords()
    {
        // 4. SEC-07 verification: Tenant B cannot access Tenant A records
        var tenantAClient = CreateAuthenticatedClient(EdithRoles.Mlro, InMemoryTenantRegistry.DefaultTenantId);
        var tenantBClient = CreateAuthenticatedClient(EdithRoles.Mlro, InMemoryTenantRegistry.SecondaryTenantId);

        // Tenant A screens a transaction to generate a case in Tenant A
        var refNo = $"TX-TENANT-A-{Guid.NewGuid():N}";
        using var screenHttp2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screen/transaction")
        {
            Content = JsonContent.Create(new
            {
                transactionRefNo = refNo,
                amount = 900000m,
                currency = "USD",
                senderName = "Tenant A Trading",
                beneficiaryName = "Viktor Anatolyevich Bout",
                beneficiaryCountry = "RU"
            })
        };
        screenHttp2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var screenRes = await tenantAClient.SendAsync(screenHttp2);
        Assert.Equal(HttpStatusCode.OK, screenRes.StatusCode);

        // Find the case ID in Tenant A
        var casesARes = await tenantAClient.GetAsync("/api/v1/cases");
        var casesAJson = await casesARes.Content.ReadAsStringAsync();
        using var casesADoc = JsonDocument.Parse(casesAJson);
        var caseItem = casesADoc.RootElement.EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("primarySubjectName").GetString() == "Viktor Anatolyevich Bout");
        Assert.True(caseItem.ValueKind == JsonValueKind.Object);
        var caseIdA = Guid.Parse(caseItem.GetProperty("id").GetString()!);

        // Tenant B requests Tenant A's case -> MUST return 404 NotFound
        var crossTenantGetRes = await tenantBClient.GetAsync($"/api/v1/cases/{caseIdA}");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantGetRes.StatusCode);

        // Tenant B requests cases list -> Tenant A's case must NOT appear in Tenant B's results
        var casesBRes = await tenantBClient.GetAsync("/api/v1/cases");
        var casesBJson = await casesBRes.Content.ReadAsStringAsync();
        using var casesBDoc = JsonDocument.Parse(casesBJson);
        var existsInB = casesBDoc.RootElement.EnumerateArray()
            .Any(c => c.GetProperty("id").GetString() == caseIdA.ToString());
        Assert.False(existsInB, "Tenant A case was leaked in Tenant B case list!");
    }

    [Fact]
    public async Task Req05_ForgedTenantHeader_MismatchedFromJwtClaim_ReturnsForbidden()
    {
        // 5. Forging an X-Tenant-ID header that differs from the token claim MUST return 403 Forbidden
        var token = GenerateToken(EdithRoles.Mlro, InMemoryTenantRegistry.DefaultTenantId);
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cases");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // Header conflicts with JWT tenant
        req.Headers.Add("X-Tenant-ID", InMemoryTenantRegistry.SecondaryTenantId.ToString());

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("Tenant Access Denied", body);
    }

    [Fact]
    public async Task Req06_InvalidJwt_ReturnsUnauthorized()
    {
        // 6. Invalid / forged JWT signatures must be rejected with 401
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cases");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.bogusPayload.invalidSignature");

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Req07_ExpiredJwt_ReturnsUnauthorized()
    {
        // 7. Expired JWT tokens must be rejected with 401
        var expiredToken = GenerateToken(EdithRoles.Mlro, InMemoryTenantRegistry.DefaultTenantId, lifetime: TimeSpan.FromMinutes(-15));
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cases");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Req08_AuditRecords_UseAuthenticatedIdentity()
    {
        // 8. Audit ledger entries record the authenticated actor name
        var adminClient = CreateAuthenticatedClient(EdithRoles.SuperAdmin);

        var updateReq = new
        {
            nameWeightage = 60m,
            nationalityWeightage = 20m,
            dobWeightage = 10m,
            idWeightage = 10m,
            defaultThreshold = 80m
        };
        var updateRes = await adminClient.PutAsJsonAsync("/api/v1/settings/scoring-weights", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // Query audit ledger
        var auditRes = await adminClient.GetAsync("/api/v1/reports/audit?limit=5");
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);
        var auditJson = await auditRes.Content.ReadAsStringAsync();
        using var auditDoc = JsonDocument.Parse(auditJson);
        Assert.True(auditDoc.RootElement.GetArrayLength() > 0);

        var latest = auditDoc.RootElement.EnumerateArray().First();
        Assert.Equal("superadmin@edith.aml.cloud", latest.GetProperty("actor").GetString());
    }

    [Fact]
    public void Req09_MissingProductionSecrets_PreventsApplicationStartup()
    {
        // 9. SEC-03 verification: SecretsConfigurationManager throws in Production when secrets are missing or default
        var emptyConfig = new ConfigurationBuilder().Build();
        var prodEnvironment = new TestHostEnvironment { EnvironmentName = "Production" };

        Action actEmpty = () => SecretsConfigurationManager.ValidateProductionConfiguration(emptyConfig, prodEnvironment);
        var ex = Assert.Throws<InvalidOperationException>(actEmpty);

        Assert.Contains("CRITICAL SECURITY CONFIGURATION ERROR", ex.Message);

        // Verification with valid production secrets passes
        var validProdConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_SECRET"] = "ProductionGradeSecretKeyWithAtLeast32BytesLength!",
                ["EDITH_PG_CONNECTION"] = "Host=prod-db.internal;Database=edith_aml;Username=edith_user;Password=ComplexPassword123!"
            })
            .Build();

        // Must not throw with valid secrets
        SecretsConfigurationManager.ValidateProductionConfiguration(validProdConfig, prodEnvironment);
    }

    [Fact]
    public async Task Req10_LegitimateLoginAndMfa_WorkflowSucceeds()
    {
        // 10. Legitimate login + MFA workflow continues working properly
        // Step A: Login challenge
        var loginReq = new LoginRequest("istr.mlro@adcb.ae", "EdithCompliance2026!");
        var loginRes = await _client.PostAsJsonAsync("/api/v1/iam/login", loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var loginBody = await loginRes.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        Assert.NotNull(loginBody);
        Assert.Equal("MFA_REQUIRED", loginBody.Status);
        Assert.False(string.IsNullOrWhiteSpace(loginBody.MfaSessionToken));

        // Step B: Verify TOTP 2FA code
        using var scope = _factory.Services.CreateScope();
        var totpService = scope.ServiceProvider.GetRequiredService<ITotpService>();
        var currentCode = totpService.GenerateCode("JBSWY3DPEHPK3PXP");

        var verifyReq = new MfaVerifyRequest(loginBody.MfaSessionToken, currentCode);
        var verifyRes = await _client.PostAsJsonAsync("/api/v1/iam/mfa/verify", verifyReq);
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);

        var verifyBody = await verifyRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = verifyBody.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        // Step C: Use issued JWT to call protected endpoints
        using var authReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/iam/entitlements");
        authReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var entRes = await _client.SendAsync(authReq);
        Assert.Equal(HttpStatusCode.OK, entRes.StatusCode);

        using var casesReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cases");
        casesReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var casesRes = await _client.SendAsync(casesReq);
        Assert.Equal(HttpStatusCode.OK, casesRes.StatusCode);
    }

    [Fact]
    public async Task Req12_LogoutAndRefreshEndpoints_WorkCorrectly()
    {
        var client = CreateAuthenticatedClient(EdithRoles.AmlOfficer);

        // 1. Silent Token Refresh
        var refreshRes = await client.PostAsync("/api/v1/iam/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);

        var refreshBody = await refreshRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var newAccessToken = refreshBody.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(newAccessToken));
        Assert.Equal("SUCCESS", refreshBody.GetProperty("status").GetString());

        // 2. Server-Side Logout
        var logoutRes = await client.PostAsync("/api/v1/iam/logout", null);
        Assert.Equal(HttpStatusCode.OK, logoutRes.StatusCode);

        var logoutBody = await logoutRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(logoutBody.GetProperty("success").GetBoolean());
    }

    private sealed class TestHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Edith.Gateway";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

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

        // Security headers verification
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
    }

    [Fact]
    public async Task CaseResolution_With_Ssrf_Url_Is_Rejected()
    {
        var caseId = Guid.NewGuid();
        var resolveReq = new
        {
            resolution = "ClearedFalsePositive",
            rationale = "Testing SSRF guard",
            cbsWebhookUrl = "http://169.254.169.254/latest/meta-data/"
        };
        var res = await _client.PostAsJsonAsync($"/api/v1/cases/{caseId}/resolve", resolveReq);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("InvalidWebhookUrl", body);
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
        var caseMenu = entitlements.Menus.FirstOrDefault(m => m.Key is "cases" or "MENU_STR_PROCESS");
        Assert.NotNull(caseMenu);
        Assert.Contains("dispose", caseMenu.AllowedActions);
        Assert.Contains("freeze", caseMenu.AllowedActions);
        Assert.Contains("str_export", caseMenu.AllowedActions);

        // MLRO should NOT have system masters/admin menu
        Assert.DoesNotContain(entitlements.Menus, m => m.Key is "masters" or "MENU_MASTER" or "MENU_SETTINGS");
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
        var caseMenu = entitlements.Menus.FirstOrDefault(m => m.Key is "cases" or "MENU_STR_PROCESS");
        Assert.NotNull(caseMenu);
        Assert.DoesNotContain("dispose", caseMenu.AllowedActions);
        Assert.DoesNotContain("freeze", caseMenu.AllowedActions);
        Assert.Contains("recommend", caseMenu.AllowedActions);

        // Analyst must NOT have rules builder or masters menu
        Assert.DoesNotContain(entitlements.Menus, m => m.Key is "rules" or "MENU_AML");
        Assert.DoesNotContain(entitlements.Menus, m => m.Key is "masters" or "MENU_MASTER" or "MENU_SETTINGS");
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

        // 3. Analyst acquires pessimistic lease lock with custom renewal duration (60 minutes)
        var lockRes = await _client.PostAsync($"/api/v1/cases/{caseId}/acquire-lock?durationMinutes=60", null);
        Assert.Equal(HttpStatusCode.OK, lockRes.StatusCode);
        var lockJson = await lockRes.Content.ReadAsStringAsync();
        using var lockDoc = JsonDocument.Parse(lockJson);
        Assert.True(lockDoc.RootElement.GetProperty("isLocked").GetBoolean());
        var expiresAt = DateTimeOffset.Parse(lockDoc.RootElement.GetProperty("expiresAt").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(expiresAt > DateTimeOffset.UtcNow.AddMinutes(50));

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

    [Fact]
    public async Task Iam_User_And_Group_Crud_With_Pbkdf2_And_Screening_Enhancements_Work()
    {
        // 1. Verify PBKDF2-SHA256 password hasher
        var hash = IamPasswordHasher.HashPassword("ComplianceSecret#2026");
        Assert.StartsWith("pbkdf2_sha256$", hash);
        Assert.True(IamPasswordHasher.VerifyPassword("ComplianceSecret#2026", hash));
        Assert.False(IamPasswordHasher.VerifyPassword("WrongPassword", hash));

        // 2. Provision new IAM user
        var createUserRes = await _client.PostAsJsonAsync("/api/v1/iam/users", new CreateIamUserRequest(
            "nadia.alhashimi@adcb.ae",
            "ComplianceSecret#2026",
            "Nadia Al-Hashimi",
            "ANALYST",
            null,
            "Sanctions Triage Unit",
            "ON_DUTY"));
        Assert.Equal(HttpStatusCode.Created, createUserRes.StatusCode);
        var createPayload = await createUserRes.Content.ReadFromJsonAsync<CreateIamUserResponse>(JsonOptions);
        Assert.NotNull(createPayload);
        var createdUser = createPayload.User;
        Assert.NotNull(createdUser);
        Assert.Equal("Nadia Al-Hashimi", createdUser.FullName);

        // 3. Update IAM user shift status and role
        var updateUserRes = await _client.PutAsJsonAsync($"/api/v1/iam/users/{createdUser.Id}", new UpdateIamUserRequest(
            Role: "MLRO",
            ShiftStatus: "OFF_DUTY"));
        Assert.Equal(HttpStatusCode.OK, updateUserRes.StatusCode);

        // 4. Create IAM Group & Update Permissions
        var createGroupRes = await _client.PostAsJsonAsync("/api/v1/iam/groups", new CreateIamGroupRequest(
            "SENIOR_SANCTIONS_DESK",
            [new IamGroupPermissionDto("MENU_SCREENING", true, true, true)]));
        Assert.Equal(HttpStatusCode.Created, createGroupRes.StatusCode);
        var createdGroup = await createGroupRes.Content.ReadFromJsonAsync<IamGroupDto>(JsonOptions);
        Assert.NotNull(createdGroup);

        var updatePermsRes = await _client.PutAsJsonAsync($"/api/v1/iam/groups/{createdGroup.Id}/permissions", new UpdateGroupPermissionsRequest(
            [
                new IamGroupPermissionDto("MENU_SCREENING", true, true, true),
                new IamGroupPermissionDto("MENU_CASES", true, true, false)
            ]));
        Assert.Equal(HttpStatusCode.OK, updatePermsRes.StatusCode);

        // 5. Direct 1-Click Intercept Case Creation & Audit Certificate
        var interceptRes = await _client.PostAsJsonAsync("/api/v1/screen/intercept-case", new
        {
            beneficiaryName = "Viktor Anatolyevich Bout",
            riskScore = 96.5m,
            triggeredList = "OFAC_SDN",
            holdReason = "Direct 1-Click Intercept from Single Entity Screening"
        });
        Assert.Equal(HttpStatusCode.Created, interceptRes.StatusCode);

        var certRes = await _client.PostAsJsonAsync("/api/v1/screen/audit-certificate", new
        {
            subjectName = "Viktor Anatolyevich Bout",
            subjectId = "OFAC-9812",
            screeningType = "SINGLE_ENTITY",
            decision = "HELD",
            overallScore = 96.5m,
            latencyMs = 4.2,
            matchCount = 1
        });
        Assert.Equal(HttpStatusCode.OK, certRes.StatusCode);
        var certJson = await certRes.Content.ReadAsStringAsync();
        Assert.Contains("wormBlockHashSha256", certJson);

        // 6. Malformed JSON request is caught by GlobalErrorHandlingMiddleware and returns general error with EDITH-ERR reference
        using var badReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/iam/login")
        {
            Content = new StringContent("{ invalid json", System.Text.Encoding.UTF8, "application/json")
        };
        var badRes = await _client.SendAsync(badReq);
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);
        var badBody = await badRes.Content.ReadAsStringAsync();
        Assert.Contains("EDITH-ERR-", badBody);
        Assert.Contains("Invalid Information", badBody);
    }

    [Fact]
    public async Task Kyc360_Directory_Dossier_And_Edd_Workflow_Updates_Succeed()
    {
        // 1. List KYC dossiers
        var listRes = await _client.GetAsync("/api/v1/kyc");
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<List<KycDossierDto>>(JsonOptions);
        Assert.NotNull(list);
        Assert.NotEmpty(list);

        // 2. Lookup specific dossier by code
        var getRes = await _client.GetAsync("/api/v1/kyc/CUST-AE-88902");
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var dossier = await getRes.Content.ReadFromJsonAsync<KycDossierDto>(JsonOptions);
        Assert.NotNull(dossier);
        Assert.Equal("CUST-AE-88902", dossier.CustomerCode);
        Assert.NotEmpty(dossier.UboIdentities);
        Assert.NotEmpty(dossier.Documents);

        // 3. Complete Periodic EDD Review Sign-Off
        var eddRes = await _client.PutAsJsonAsync("/api/v1/kyc/CUST-AE-88902/edd", new KycEddUpdateRequest(
            Action: "COMPLETE_EDD_REVIEW",
            RiskTier: "HIGH",
            NextReviewDate: "2027-10-25",
            Notes: "Periodic EDD Review signed off in integration test."));
        Assert.Equal(HttpStatusCode.OK, eddRes.StatusCode);
        var updated = await eddRes.Content.ReadFromJsonAsync<KycDossierDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("EDD_APPROVED", updated.OnboardingStatus);
    }

    [Fact]
    public async Task Rules_Crud_DraftBacktest_And_Audit_Workflow_Succeeds()
    {
        // 1. Create a structuring rule in SHADOW mode
        var createRes = await _client.PostAsJsonAsync("/api/v1/rules", new
        {
            ruleCode = "RULE_TEST_SMURF_09",
            ruleName = "Test Structuring Avoidance Band",
            ruleType = "STRUCTURING_SMURFING",
            severity = "CRITICAL",
            weightage = 45m,
            executionMode = "SHADOW",
            isActive = true,
            currency = "AED",
            windowMinutes = 1440,
            minTransactions = 3,
            structuringMinAmount = 45000m,
            structuringMaxAmount = 54999m
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        var createdJson = await createRes.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdJson);
        var ruleId = createdDoc.RootElement.GetProperty("id").GetString()!;
        Assert.Equal("STRUCTURING_SMURFING", createdDoc.RootElement.GetProperty("ruleType").GetString());
        Assert.Equal("SHADOW", createdDoc.RootElement.GetProperty("executionMode").GetString());
        Assert.Equal(45000m, createdDoc.RootElement.GetProperty("structuringMinAmount").GetDecimal());
        Assert.Equal(54999m, createdDoc.RootElement.GetProperty("structuringMaxAmount").GetDecimal());

        // 2. Promote rule to REALTIME via PUT
        var updateRes = await _client.PutAsJsonAsync($"/api/v1/rules/{ruleId}", new
        {
            ruleCode = "RULE_TEST_SMURF_09",
            ruleName = "Test Structuring Avoidance Band",
            ruleType = "STRUCTURING_SMURFING",
            severity = "CRITICAL",
            weightage = 50m,
            executionMode = "REALTIME",
            isActive = true,
            currency = "AED",
            windowMinutes = 1440,
            minTransactions = 3,
            structuringMinAmount = 40000m,
            structuringMaxAmount = 54999m
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updatedJson = await updateRes.Content.ReadAsStringAsync();
        using var updatedDoc = JsonDocument.Parse(updatedJson);
        Assert.Equal("REALTIME", updatedDoc.RootElement.GetProperty("executionMode").GetString());
        Assert.Equal(40000m, updatedDoc.RootElement.GetProperty("structuringMinAmount").GetDecimal());

        // 3. Run Draft What-If Backtest with custom overrides
        var backtestRes = await _client.PostAsJsonAsync("/api/v1/rules/backtest", new
        {
            ruleCode = "RULE_TEST_SMURF_09",
            ruleType = "TRANSACTION_THRESHOLD",
            thresholdAmount = 250000m,
            currency = "AED"
        });
        Assert.Equal(HttpStatusCode.OK, backtestRes.StatusCode);
        var btJson = await backtestRes.Content.ReadAsStringAsync();
        using var btDoc = JsonDocument.Parse(btJson);
        Assert.Equal(1000, btDoc.RootElement.GetProperty("simulatedTransactions").GetInt32());
        Assert.True(btDoc.RootElement.GetProperty("triggeredAlerts").GetInt32() > 0);
        Assert.True(btDoc.RootElement.GetProperty("sampleHits").GetArrayLength() > 0);

        // 4. Delete rule
        var delRes = await _client.DeleteAsync($"/api/v1/rules/{ruleId}");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);
    }

    [Fact]
    public async Task Notification_Admin_Rules_Ledgers_And_TestFire_Workflow_Succeeds()
    {
        // 1. Fetch notification dispatch rules
        var rulesRes = await _client.GetAsync("/api/v1/notifications/rules");
        var rulesJson = await rulesRes.Content.ReadAsStringAsync();
        Assert.True(rulesRes.IsSuccessStatusCode, $"Status: {rulesRes.StatusCode}, Body: {rulesJson}");
        using var rulesDoc = JsonDocument.Parse(rulesJson);
        Assert.True(rulesDoc.RootElement.GetArrayLength() > 0);

        // 2. Create custom notification rule
        var createReq = new
        {
            name = "Test Webhook Relay",
            triggerEvent = "Transaction Frozen (OFAC / UN Hit)",
            severity = "CRITICAL",
            channels = new[] { "TEAMS", "WEBHOOK" },
            targetEndpoint = "https://cbs-relay.test.adcb.ae/api/v1/aml-callback",
            fallbackRecipient = "MLRO Duty Officer",
            throttleMinutes = 0
        };
        var createRes = await _client.PostAsJsonAsync("/api/v1/notifications/rules", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdRule = await createRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var ruleId = createdRule.GetProperty("id").GetString()!;

        // 3. Toggle rule active state
        var toggleRes = await _client.PatchAsync($"/api/v1/notifications/rules/{ruleId}/toggle", null);
        Assert.Equal(HttpStatusCode.OK, toggleRes.StatusCode);

        // 4. Test-fire simulated notification dispatch
        var fireRes = await _client.PostAsJsonAsync("/api/v1/notifications/test-fire", new
        {
            ruleId,
            channel = "TEAMS",
            customEndpoint = "https://cbs-relay.test.adcb.ae/api/v1/aml-callback"
        });
        Assert.Equal(HttpStatusCode.OK, fireRes.StatusCode);
        var fireJson = await fireRes.Content.ReadAsStringAsync();
        Assert.Contains("Test dispatch delivered", fireJson);

        // 5. Query delivery ledgers and shift roster
        var ledgersRes = await _client.GetAsync("/api/v1/notifications/ledgers");
        Assert.Equal(HttpStatusCode.OK, ledgersRes.StatusCode);

        var shiftsRes = await _client.GetAsync("/api/v1/notifications/shifts");
        Assert.Equal(HttpStatusCode.OK, shiftsRes.StatusCode);

        // 6. Query inbox notifications
        var inboxRes = await _client.GetAsync("/api/v1/notifications/inbox");
        Assert.Equal(HttpStatusCode.OK, inboxRes.StatusCode);
    }

    [Fact]
    public async Task Delta_Sync_Adjudication_And_AuditReport_Workflow_Succeeds()
    {
        // 1. Fetch Delta Sources
        var sourcesRes = await _client.GetAsync("/api/v1/delta/sources");
        Assert.Equal(HttpStatusCode.OK, sourcesRes.StatusCode);
        var sourcesJson = await sourcesRes.Content.ReadAsStringAsync();
        using var sourcesDoc = JsonDocument.Parse(sourcesJson);
        Assert.True(sourcesDoc.RootElement.GetArrayLength() >= 3);

        // 2. Trigger Delta Rescreening Sync Batch
        var syncRes = await _client.PostAsync("/api/v1/delta/sync", null);
        Assert.Equal(HttpStatusCode.OK, syncRes.StatusCode);
        var syncJson = await syncRes.Content.ReadAsStringAsync();
        using var syncDoc = JsonDocument.Parse(syncJson);
        var batchId = syncDoc.RootElement.GetProperty("syncBatchId").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(batchId));

        // 3. Adjudicate a Delta Hit (Dismiss false positive)
        var adjRes = await _client.PostAsJsonAsync("/api/v1/delta/adjudicate", new
        {
            hitId = "hit-test-001",
            customerCode = "CUST-AE-88902",
            decision = "DISMISS",
            rationale = "Customer secondary identification cleared",
            batchId
        });
        Assert.Equal(HttpStatusCode.OK, adjRes.StatusCode);

        // 4. Query Delta Log Report
        var logRes = await _client.GetAsync("/api/v1/delta/log-report");
        Assert.Equal(HttpStatusCode.OK, logRes.StatusCode);
    }

    [Fact]
    public async Task Audit_Ledger_WORM_Record_And_Query_Integrity_Succeeds()
    {
        // 1. Record an immutable audit log entry
        var entryReq = new
        {
            actor = "Integration Tester",
            action = "SECURITY_VALIDATION_EVENT",
            targetEntity = "Core Gateway Router",
            caseNumber = "EDITH-CAS-TEST-01",
            details = "Verified cryptographic WORM append operation."
        };
        var recordRes = await _client.PostAsJsonAsync("/api/v1/reports/audit", entryReq);
        Assert.Equal(HttpStatusCode.OK, recordRes.StatusCode);
        var recordJson = await recordRes.Content.ReadAsStringAsync();
        using var recordDoc = JsonDocument.Parse(recordJson);
        var hash = recordDoc.RootElement.GetProperty("sha256Hash").GetString();
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);

        // 2. Query audit logs and verify the recorded event exists
        var queryRes = await _client.GetAsync("/api/v1/reports/audit?limit=10");
        Assert.Equal(HttpStatusCode.OK, queryRes.StatusCode);
        var queryJson = await queryRes.Content.ReadAsStringAsync();
        Assert.Contains("SECURITY_VALIDATION_EVENT", queryJson);
        Assert.Contains("Integration Tester", queryJson);
    }

    [Fact]
    public async Task Settings_Masters_Scoring_Weights_And_Country_Risk_Succeeds()
    {
        // 1. Fetch System Masters
        var mastersRes = await _client.GetAsync("/api/v1/settings/masters");
        Assert.Equal(HttpStatusCode.OK, mastersRes.StatusCode);
        var mastersJson = await mastersRes.Content.ReadAsStringAsync();
        using var mastersDoc = JsonDocument.Parse(mastersJson);
        Assert.True(mastersDoc.RootElement.TryGetProperty("scoringWeights", out var weights));
        Assert.True(weights.GetProperty("nameWeightage").GetDecimal() > 0);

        // 2. Update Scoring Weights
        var updateWeightsRes = await _client.PutAsJsonAsync("/api/v1/settings/scoring-weights", new
        {
            nameWeightage = 65m,
            nationalityWeightage = 15m,
            dobWeightage = 10m,
            idWeightage = 10m,
            defaultThreshold = 82m
        });
        Assert.Equal(HttpStatusCode.OK, updateWeightsRes.StatusCode);

        // 3. Upsert Country Risk Master
        var countryRes = await _client.PostAsJsonAsync("/api/v1/settings/countries", new
        {
            code = "TST",
            countryName = "Testlandia",
            isHighRisk = true,
            riskScore = 88.5m
        });
        Assert.Equal(HttpStatusCode.OK, countryRes.StatusCode);
    }
}

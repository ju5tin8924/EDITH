using System.Net;
using System.Text.Json;
using Edith.Events.Cases;
using Edith.Events.Contracts;
using Edith.Events.Kafka;
using Edith.Events.Outbox;
using Edith.Events.Webhooks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Edith.Events.Tests;

public sealed class EventsTests
{
    private static readonly Guid TestTenantId = Guid.Parse("01931000-0000-7000-8000-000000000001");

    [Fact]
    public async Task Outbox_Appends_And_Processes_Pending_Messages()
    {
        var repo = new InMemoryOutboxRepository();
        var eventId = Guid.NewGuid();

        var message = new OutboxMessage
        {
            Id = eventId,
            TenantId = TestTenantId,
            EventType = nameof(TransactionHeldEvent),
            AggregateId = Guid.NewGuid(),
            Payload = JsonSerializer.Serialize(new { TxRef = "TX-999" })
        };

        await repo.AppendMessageAsync(message);

        var pending = await repo.FetchPendingMessagesAsync();
        Assert.Single(pending);
        Assert.Equal("PENDING", pending[0].Status);

        await repo.MarkProcessedAsync(eventId);

        var remainingPending = await repo.FetchPendingMessagesAsync();
        Assert.Empty(remainingPending);
    }

    [Fact]
    public async Task EventBus_Publishes_And_Dispatches_TransactionHeldEvent()
    {
        var bus = new InMemoryEventBus();
        TransactionHeldEvent? receivedEvent = null;

        bus.Subscribe<TransactionHeldEvent>(EdithTopics.TransactionHeld, (evt, _) =>
        {
            receivedEvent = evt;
            return Task.CompletedTask;
        });

        var sentEvent = new TransactionHeldEvent
        {
            EventId = Guid.NewGuid(),
            TenantId = TestTenantId,
            TransactionId = Guid.NewGuid(),
            TransactionRefNo = "TX-HELD-001",
            Amount = 150000m,
            Currency = "AED",
            SenderName = "Acme Trading FZE",
            BeneficiaryName = "Viktor Anatolyevich Bout",
            OverallScore = 96.50m,
            HoldReason = "SANCTION_MATCH on Beneficiary",
            CaseNumber = "CASE-20261011-001"
        };

        await bus.PublishAsync(EdithTopics.TransactionHeld, sentEvent.TenantId.ToString(), sentEvent);

        Assert.NotNull(receivedEvent);
        Assert.Equal("TX-HELD-001", receivedEvent.TransactionRefNo);
        Assert.Equal(96.50m, receivedEvent.OverallScore);
    }

    [Theory]
    [InlineData(95.0, "CRITICAL")]
    [InlineData(85.0, "HIGH")]
    [InlineData(72.0, "MEDIUM")]
    public async Task CaseAutoCreator_Assigns_Correct_Priority_And_3Day_Sla(double score, string expectedPriority)
    {
        var caseRepo = new InMemoryCaseRepository();
        var autoCreator = new CaseAutoCreator(caseRepo);

        var now = DateTimeOffset.UtcNow;
        var evt = new TransactionHeldEvent
        {
            EventId = Guid.NewGuid(),
            TenantId = TestTenantId,
            TransactionId = Guid.NewGuid(),
            TransactionRefNo = "TX-CASE-001",
            Amount = 200000m,
            Currency = "USD",
            SenderName = "Dubai Global FZE",
            BeneficiaryName = "Sanctioned Entity",
            OverallScore = (decimal)score,
            HoldReason = "High-risk entity detected",
            CaseNumber = "CASE-20261011-998877",
            Timestamp = now
        };

        var createdCase = await autoCreator.HandleTransactionHeldAsync(evt);

        Assert.NotNull(createdCase);
        Assert.Equal("OPEN", createdCase.Status);
        Assert.Equal(expectedPriority, createdCase.Priority);
        Assert.Equal("CASE-20261011-998877", createdCase.CaseNumber);
        Assert.Equal(now.AddDays(3), createdCase.SlaDueDate);

        var foundInRepo = await caseRepo.FindByCaseNumberAsync(TestTenantId, "CASE-20261011-998877");
        Assert.NotNull(foundInRepo);
        Assert.Equal(createdCase.Id, foundInRepo.Id);
    }

    [Fact]
    public void HmacSignatureHelper_Generates_And_Validates_Signatures_Correctly()
    {
        const string payload = "{\"transactionRefNo\":\"TX-1002345\",\"decision\":\"FREEZE\"}";
        const string secret = "CBS_SUPER_SECRET_HMAC_KEY_12345";

        var signatureHex = HmacSignatureHelper.ComputeSignature(payload, secret);
        Assert.False(string.IsNullOrWhiteSpace(signatureHex));

        // 1. Valid signature with prefix
        Assert.True(HmacSignatureHelper.VerifySignature(payload, secret, $"sha256={signatureHex}"));

        // 2. Valid signature raw hex
        Assert.True(HmacSignatureHelper.VerifySignature(payload, secret, signatureHex));

        // 3. Wrong secret
        Assert.False(HmacSignatureHelper.VerifySignature(payload, "WRONG_SECRET", signatureHex));

        // 4. Tampered payload
        const string tampered = "{\"transactionRefNo\":\"TX-1002345\",\"decision\":\"RELEASE\"}";
        Assert.False(HmacSignatureHelper.VerifySignature(tampered, secret, signatureHex));
    }

    [Fact]
    public async Task CbsWebhookDispatcher_Sends_Signed_Webhook_To_Cbs()
    {
        const string sharedSecret = "CBS_TEST_SECRET_KEY_998877";
        var capturedHeaders = new Dictionary<string, string>();
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(async request =>
        {
            foreach (var h in request.Headers)
            {
                capturedHeaders[h.Key] = string.Join(";", h.Value);
            }
            if (request.Content != null)
            {
                capturedBody = await request.Content.ReadAsStringAsync();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"acknowledged\":true}")
            };
        });

        using var httpClient = new HttpClient(handler);
        var dispatcher = new CbsWebhookDispatcher(httpClient, NullLogger<CbsWebhookDispatcher>.Instance);

        var payload = new AmlDecisionWebhookPayload
        {
            TenantId = TestTenantId,
            TransactionRefNo = "TX-DISPATCH-001",
            Decision = "FREEZE",
            Reason = "Confirmed OFAC SDN Sanction match after MLRO investigation",
            CaseNumber = "CASE-20261011-0099",
            DecidedBy = "MLRO_FATIMA"
        };

        var result = await dispatcher.DispatchDecisionWebhookAsync(
            payload,
            "https://cbs.internal.bank/core/webhooks/aml-decision",
            sharedSecret);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("acknowledged", result.ResponsePayload);

        // Verify headers dispatched to CBS
        Assert.True(capturedHeaders.ContainsKey("X-Edith-Signature"));
        Assert.True(capturedHeaders.ContainsKey("X-Edith-Timestamp"));
        Assert.Equal("AML_DECISION", capturedHeaders["X-Edith-Event"]);
        Assert.Equal(TestTenantId.ToString(), capturedHeaders["X-Tenant-ID"]);

        // Verify HMAC signature by CBS receiver
        var sigHeader = capturedHeaders["X-Edith-Signature"];
        Assert.NotNull(capturedBody);
        Assert.True(HmacSignatureHelper.VerifySignature(capturedBody, sharedSecret, sigHeader));
    }

    [Fact]
    public async Task EndToEnd_Held_Flow_Creates_Outbox_Publishes_Creates_Case_And_Dispatches_Webhook()
    {
        // 1. Setup Outbox & Event Bus
        var outboxRepo = new InMemoryOutboxRepository();
        var eventBus = new InMemoryEventBus();
        var caseRepo = new InMemoryCaseRepository();
        var autoCreator = new CaseAutoCreator(caseRepo);

        // 2. Wire subscription from Kafka topic to Case Auto-Creator
        eventBus.Subscribe<TransactionHeldEvent>(EdithTopics.TransactionHeld, async (evt, ct) =>
        {
            await autoCreator.HandleTransactionHeldAsync(evt, ct);
        });

        // 3. Screening yields HELD -> Appends to Outbox
        var heldEvent = new TransactionHeldEvent
        {
            EventId = Guid.NewGuid(),
            TenantId = TestTenantId,
            TransactionId = Guid.NewGuid(),
            TransactionRefNo = "TX-E2E-001",
            Amount = 1000000m,
            Currency = "USD",
            SenderName = "Shell Company Ltd",
            BeneficiaryName = "Viktor Anatolyevich Bout",
            OverallScore = 98.0m,
            HoldReason = "OFAC_SDN SANCTION MATCH 98%",
            CaseNumber = "CASE-E2E-9999"
        };

        await outboxRepo.AppendMessageAsync(new OutboxMessage
        {
            Id = heldEvent.EventId,
            TenantId = heldEvent.TenantId,
            EventType = nameof(TransactionHeldEvent),
            AggregateId = heldEvent.TransactionId,
            Payload = JsonSerializer.Serialize(heldEvent)
        });

        // 4. Outbox Publisher processes pending messages
        var processor = new OutboxProcessor(outboxRepo, eventBus, NullLogger<OutboxProcessor>.Instance);
        var processed = await processor.ProcessPendingOutboxMessagesAsync();
        Assert.Equal(1, processed);

        // 5. Verify Case was auto-created in Case Management
        var createdCase = await caseRepo.FindByCaseNumberAsync(TestTenantId, "CASE-E2E-9999");
        Assert.NotNull(createdCase);
        Assert.Equal("CRITICAL", createdCase.Priority);
        Assert.Equal("OPEN", createdCase.Status);
        Assert.Equal(98.0m, createdCase.RiskScore);

        // 6. MLRO reviews and Freezes -> Webhook dispatched to CBS
        const string cbsSecret = "CBS_E2E_SECRET";
        var webhookDelivered = false;

        var handler = new MockHttpMessageHandler(async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            var sig = req.Headers.GetValues("X-Edith-Signature").First();
            webhookDelivered = HmacSignatureHelper.VerifySignature(body, cbsSecret, sig);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"ACCOUNT_FROZEN\"}")
            };
        });

        using var http = new HttpClient(handler);
        var dispatcher = new CbsWebhookDispatcher(http, NullLogger<CbsWebhookDispatcher>.Instance);

        var webhookPayload = new AmlDecisionWebhookPayload
        {
            TenantId = TestTenantId,
            TransactionRefNo = heldEvent.TransactionRefNo,
            Decision = "FREEZE",
            Reason = "Confirmed Sanction Match",
            CaseNumber = createdCase.CaseNumber,
            DecidedBy = "MLRO_TARIQ"
        };

        var webhookResult = await dispatcher.DispatchDecisionWebhookAsync(
            webhookPayload,
            "https://cbs.bank/webhooks/aml",
            cbsSecret);

        Assert.True(webhookResult.Success);
        Assert.True(webhookDelivered);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}

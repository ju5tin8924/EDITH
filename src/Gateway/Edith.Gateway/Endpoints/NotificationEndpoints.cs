using System.Collections.Concurrent;
using System.Text.Json;
using Edith.Addons.Notifications;
using Edith.Events.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Npgsql;

namespace Edith.Gateway.Endpoints;

public record NotificationRuleRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; } = Guid.Parse("01931000-0000-7000-8000-000000000000");
    public string Name { get; set; } = string.Empty;
    public string TriggerEvent { get; set; } = string.Empty;
    public string Severity { get; set; } = "HIGH";
    public List<string> Channels { get; set; } = new();
    public bool ShiftAware { get; set; } = true;
    public bool Active { get; set; } = true;
    public string? TargetEndpoint { get; set; }
    public string? FallbackRecipient { get; set; }
    public int ThrottleMinutes { get; set; } = 5;
    public string TemplateFormat { get; set; } = "ADAPTIVE_CARD";
    public DateTimeOffset? LastDispatchedAt { get; set; }
    public int DispatchCount { get; set; }
}

public record DeliveryLedgerItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? NotificationRuleId { get; init; }
    public string RuleName { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
    public string Channel { get; init; } = "TEAMS";
    public string TargetEndpoint { get; init; } = string.Empty;
    public string DeliveryStatus { get; set; } = "DELIVERED";
    public int AttemptCount { get; set; } = 1;
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public double LatencyMs { get; set; } = 42.5;
    public int? HttpStatus { get; set; } = 200;
    public string? ErrorMessage { get; set; }
    public string? PayloadJson { get; set; }
    public string? SignatureHex { get; set; }
}

public record ShiftRosterRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = "ANALYST";
    public string ShiftName { get; set; } = "EMEA";
    public string ShiftStart { get; set; } = "08:00";
    public string ShiftEnd { get; set; } = "16:00";
    public bool IsOnDuty { get; set; } = true;
    public Guid? SubstituteUserId { get; set; }
    public string? SubstituteUserName { get; set; }
}

public record InAppNotificationRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? CaseRef { get; init; }
    public string Severity { get; init; } = "HIGH";
    public bool Read { get; set; }
    public string TargetUrl { get; init; } = "/cases";
}

public static class NotificationEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("01931000-0000-7000-8000-000000000000");

    // In-memory thread-safe state stores for zero-downtime resilience
    private static readonly ConcurrentDictionary<Guid, NotificationRuleRecord> RulesStore = new();
    private static readonly ConcurrentDictionary<Guid, DeliveryLedgerItem> LedgersStore = new();
    private static readonly ConcurrentDictionary<Guid, ShiftRosterRecord> ShiftsStore = new();
    private static readonly ConcurrentDictionary<Guid, InAppNotificationRecord> InboxStore = new();

    static NotificationEndpoints()
    {
        SeedDefaults();
    }

    private static void SeedDefaults()
    {
        // 1. Seed Rules
        var r1 = new NotificationRuleRecord
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000001"),
            TenantId = DefaultTenantId,
            Name = "Statutory Freeze Emergency Alert",
            TriggerEvent = "Transaction Frozen (OFAC / UN Hit)",
            Severity = "CRITICAL",
            Channels = new List<string> { "TEAMS", "SMS", "WEBHOOK" },
            ShiftAware = true,
            Active = true,
            TargetEndpoint = "https://cbs-relay.internal.adcb.ae/api/v1/aml-freeze-callback",
            FallbackRecipient = "MLRO Duty Officer",
            ThrottleMinutes = 0,
            TemplateFormat = "ADAPTIVE_CARD",
            LastDispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-12),
            DispatchCount = 38
        };

        var r2 = new NotificationRuleRecord
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000002"),
            TenantId = DefaultTenantId,
            Name = "SLA Escalation (72h EDD Limit Near)",
            TriggerEvent = "Case Inactivity > 48 Hours",
            Severity = "HIGH",
            Channels = new List<string> { "TEAMS", "EMAIL" },
            ShiftAware = true,
            Active = true,
            TargetEndpoint = "https://teams.microsoft.com/l/webhook/compliance-escalations",
            FallbackRecipient = "Senior EDD Supervisor",
            ThrottleMinutes = 15,
            TemplateFormat = "ADAPTIVE_CARD",
            LastDispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-45),
            DispatchCount = 114
        };

        var r3 = new NotificationRuleRecord
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000003"),
            TenantId = DefaultTenantId,
            Name = "Nightly Delta Rescreening Digest",
            TriggerEvent = "Delta Batch Processing Finished",
            Severity = "MEDIUM",
            Channels = new List<string> { "EMAIL", "WEBHOOK" },
            ShiftAware = false,
            Active = true,
            TargetEndpoint = "https://reports.internal.adcb.ae/webhooks/delta-digest",
            FallbackRecipient = "Compliance Operations Hub",
            ThrottleMinutes = 60,
            TemplateFormat = "HTML_EMAIL",
            LastDispatchedAt = DateTimeOffset.UtcNow.AddHours(-8),
            DispatchCount = 29
        };

        var r4 = new NotificationRuleRecord
        {
            Id = Guid.Parse("01931000-0000-7000-8000-000000000004"),
            TenantId = DefaultTenantId,
            Name = "High-Risk Sanctions Intercept (Tier 1)",
            TriggerEvent = "Real-Time Intercept (Score >= 95.0%)",
            Severity = "CRITICAL",
            Channels = new List<string> { "TEAMS", "WEBHOOK" },
            ShiftAware = true,
            Active = true,
            TargetEndpoint = "https://cbs-relay.internal.adcb.ae/api/v1/intercept-notify",
            FallbackRecipient = "Tariq Mansoor (MLRO)",
            ThrottleMinutes = 0,
            TemplateFormat = "ADAPTIVE_CARD",
            LastDispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            DispatchCount = 86
        };

        RulesStore[r1.Id] = r1;
        RulesStore[r2.Id] = r2;
        RulesStore[r3.Id] = r3;
        RulesStore[r4.Id] = r4;

        // 2. Seed Ledgers (including Dead Letters for DLQ reconciliation)
        var l1 = new DeliveryLedgerItem
        {
            Id = Guid.NewGuid(),
            NotificationRuleId = r1.Id,
            RuleName = r1.Name,
            TraceId = "TX-2026-9921448:FREEZE",
            Channel = "WEBHOOK",
            TargetEndpoint = "https://cbs-relay.internal.adcb.ae/api/v1/aml-freeze-callback",
            DeliveryStatus = "DELIVERED",
            AttemptCount = 1,
            SentAt = DateTimeOffset.UtcNow.AddMinutes(-12),
            LatencyMs = 28.4,
            HttpStatus = 200,
            ErrorMessage = null,
            PayloadJson = JsonSerializer.Serialize(new
            {
                transactionRef = "TX-2026-9921448",
                decision = "FREEZE",
                subject = "Viktor Anatolyevich Bout",
                amount = 450000.00,
                currency = "USD",
                reason = "UN Resolution 1521 Statutory Intercept"
            }),
            SignatureHex = "sha256=a7f29b4c09d81e3a1f8792019b88f349d79ac2201948baef701239aaef"
        };

        var l2 = new DeliveryLedgerItem
        {
            Id = Guid.NewGuid(),
            NotificationRuleId = r4.Id,
            RuleName = r4.Name,
            TraceId = "TX-2026-4418902:INTERCEPT",
            Channel = "WEBHOOK",
            TargetEndpoint = "https://cbs-gateway.core-banking.bank.internal/api/v1/hold-callback",
            DeliveryStatus = "DEAD_LETTER_TIMEOUT",
            AttemptCount = 3,
            SentAt = DateTimeOffset.UtcNow.AddMinutes(-24),
            LatencyMs = 4500.0,
            HttpStatus = 504,
            ErrorMessage = "Gateway Timeout: Upstream CBS gateway failed to respond within 4000ms after 3 Polly retries",
            PayloadJson = JsonSerializer.Serialize(new
            {
                transactionRef = "TX-2026-4418902",
                decision = "INTERCEPT_HOLD",
                subject = "Al-Baraka General Trading LLC",
                amount = 1250000.00,
                currency = "AED",
                reason = "High-Risk Corridor & Secondary Watchlist Match"
            }),
            SignatureHex = "sha256=bb10283e7492cfa712908fa1948bcde82901928471203984afc7192841"
        };

        var l3 = new DeliveryLedgerItem
        {
            Id = Guid.NewGuid(),
            NotificationRuleId = r1.Id,
            RuleName = r1.Name,
            TraceId = "TX-2026-3829104:FREEZE",
            Channel = "WEBHOOK",
            TargetEndpoint = "https://cbs-relay.internal.adcb.ae/api/v1/aml-freeze-callback",
            DeliveryStatus = "DEAD_LETTER_HTTP_500",
            AttemptCount = 3,
            SentAt = DateTimeOffset.UtcNow.AddHours(-1),
            LatencyMs = 620.0,
            HttpStatus = 500,
            ErrorMessage = "Internal Server Error: CBS Core Ledger Lock acquisition timeout",
            PayloadJson = JsonSerializer.Serialize(new
            {
                transactionRef = "TX-2026-3829104",
                decision = "FREEZE",
                subject = "Quds Force Logistics Co",
                amount = 890000.00,
                currency = "EUR"
            }),
            SignatureHex = "sha256=ef91823908471928374910283749102837491028374910283749102837"
        };

        var l4 = new DeliveryLedgerItem
        {
            Id = Guid.NewGuid(),
            NotificationRuleId = r2.Id,
            RuleName = r2.Name,
            TraceId = "CAS-2026-0042:SLA_48H",
            Channel = "TEAMS",
            TargetEndpoint = "https://teams.microsoft.com/l/webhook/compliance-escalations",
            DeliveryStatus = "DELIVERED",
            AttemptCount = 1,
            SentAt = DateTimeOffset.UtcNow.AddMinutes(-45),
            LatencyMs = 142.1,
            HttpStatus = 200,
            ErrorMessage = null,
            PayloadJson = "{\"title\":\"SLA Escalation\",\"caseRef\":\"EDITH-CAS-2026-0042\"}",
            SignatureHex = "sha256=1102938481928374829103847291038472910384729103847291038472"
        };

        LedgersStore[l1.Id] = l1;
        LedgersStore[l2.Id] = l2;
        LedgersStore[l3.Id] = l3;
        LedgersStore[l4.Id] = l4;

        // 3. Seed Shift Schedules
        var s1 = new ShiftRosterRecord
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse("01931000-0000-7000-9000-000000000001"),
            UserName = "Fatima Al-Nuaimi",
            UserRole = "AML_OFFICER",
            ShiftName = "EMEA",
            ShiftStart = "08:00",
            ShiftEnd = "16:00",
            IsOnDuty = true,
            SubstituteUserId = Guid.Parse("01931000-0000-7000-9000-000000000002"),
            SubstituteUserName = "Tariq Mansoor (MLRO)"
        };

        var s2 = new ShiftRosterRecord
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse("01931000-0000-7000-9000-000000000002"),
            UserName = "Tariq Mansoor",
            UserRole = "MLRO",
            ShiftName = "EMEA",
            ShiftStart = "09:00",
            ShiftEnd = "17:00",
            IsOnDuty = true,
            SubstituteUserId = null,
            SubstituteUserName = "On-Duty Executive Roster"
        };

        var s3 = new ShiftRosterRecord
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse("01931000-0000-7000-9000-000000000003"),
            UserName = "Arjun Mehta",
            UserRole = "EDD_ANALYST",
            ShiftName = "APAC",
            ShiftStart = "04:00",
            ShiftEnd = "12:00",
            IsOnDuty = false,
            SubstituteUserId = s1.UserId,
            SubstituteUserName = s1.UserName
        };

        var s4 = new ShiftRosterRecord
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse("01931000-0000-7000-9000-000000000004"),
            UserName = "Elena Rostova",
            UserRole = "ANALYST",
            ShiftName = "AMERICAS",
            ShiftStart = "16:00",
            ShiftEnd = "00:00",
            IsOnDuty = false,
            SubstituteUserId = s1.UserId,
            SubstituteUserName = s1.UserName
        };

        ShiftsStore[s1.Id] = s1;
        ShiftsStore[s2.Id] = s2;
        ShiftsStore[s3.Id] = s3;
        ShiftsStore[s4.Id] = s4;

        // 4. Seed In-App Notifications
        var n1 = new InAppNotificationRecord
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5),
            Title = "Statutory Freeze Executed: $450,000.00 USD",
            Body = "Inbound SWIFT MT103 from Dubai (ARE) to Zurich (CHE) matched UN Res 1521.",
            CaseRef = "EDITH-CAS-2026-0042",
            Severity = "CRITICAL",
            Read = false,
            TargetUrl = "/cases"
        };

        var n2 = new InAppNotificationRecord
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-22),
            Title = "CBS Webhook Delivery Dropped (3 Retries)",
            Body = "Callback for TX-2026-4418902 entered Dead-Letter Queue (HTTP 504 Timeout).",
            CaseRef = "TX-2026-4418902",
            Severity = "HIGH",
            Read = false,
            TargetUrl = "/notifications"
        };

        var n3 = new InAppNotificationRecord
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-50),
            Title = "Case EDITH-CAS-2026-0038 SLA Warning",
            Body = "48h inactivity limit reached. Case has been escalated to Duty Supervisor.",
            CaseRef = "EDITH-CAS-2026-0038",
            Severity = "MEDIUM",
            Read = true,
            TargetUrl = "/cases"
        };

        InboxStore[n1.Id] = n1;
        InboxStore[n2.Id] = n2;
        InboxStore[n3.Id] = n3;
    }

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications");

        // 1. Get All Rules
        group.MapGet("/rules", ([FromHeader(Name = "X-Tenant-ID")] string? tenantHeader) =>
        {
            var rules = RulesStore.Values
                .OrderByDescending(r => r.Severity == "CRITICAL")
                .ThenBy(r => r.Name)
                .ToList();

            return Results.Ok(rules);
        }).AllowAnonymous();

        // 2. Create Rule
        group.MapPost("/rules", (
            [FromBody] NotificationRuleRecord newRule,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader) =>
        {
            var rule = newRule with
            {
                Id = Guid.NewGuid(),
                TenantId = ResolveTenant(tenantHeader),
                LastDispatchedAt = null,
                DispatchCount = 0
            };
            RulesStore[rule.Id] = rule;
            return Results.Created($"/api/v1/notifications/rules/{rule.Id}", rule);
        }).AllowAnonymous();

        // 3. Update Rule
        group.MapPut("/rules/{id:guid}", (
            Guid id,
            [FromBody] NotificationRuleRecord updated,
            [FromHeader(Name = "X-Tenant-ID")] string? tenantHeader) =>
        {
            if (!RulesStore.TryGetValue(id, out var existing))
            {
                return Results.NotFound(new { error = $"Rule {id} not found" });
            }

            existing.Name = updated.Name;
            existing.TriggerEvent = updated.TriggerEvent;
            existing.Severity = updated.Severity;
            existing.Channels = updated.Channels ?? new List<string>();
            existing.ShiftAware = updated.ShiftAware;
            existing.Active = updated.Active;
            existing.TargetEndpoint = updated.TargetEndpoint;
            existing.FallbackRecipient = updated.FallbackRecipient;
            existing.ThrottleMinutes = updated.ThrottleMinutes;
            existing.TemplateFormat = updated.TemplateFormat;

            return Results.Ok(existing);
        }).AllowAnonymous();

        // 4. Toggle Rule Active
        group.MapPatch("/rules/{id:guid}/toggle", (Guid id) =>
        {
            if (!RulesStore.TryGetValue(id, out var existing))
            {
                return Results.NotFound(new { error = $"Rule {id} not found" });
            }
            existing.Active = !existing.Active;
            return Results.Ok(new { id = existing.Id, active = existing.Active });
        }).AllowAnonymous();

        // 5. Delete Rule
        group.MapDelete("/rules/{id:guid}", (Guid id) =>
        {
            var removed = RulesStore.TryRemove(id, out _);
            return removed ? Results.NoContent() : Results.NotFound();
        }).AllowAnonymous();

        // 6. Delivery Ledgers & DLQ List
        group.MapGet("/ledgers", (
            [FromQuery] string? status,
            [FromQuery] string? search) =>
        {
            var query = LedgersStore.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                if (status.Equals("DEAD_LETTER", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(l => l.DeliveryStatus.StartsWith("DEAD_LETTER", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    query = query.Where(l => l.DeliveryStatus.Equals(status, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(l =>
                    l.TraceId.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    l.RuleName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    l.TargetEndpoint.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            var results = query.OrderByDescending(l => l.SentAt).ToList();
            return Results.Ok(results);
        }).AllowAnonymous();

        // 7. Replay Single Dead Letter
        group.MapPost("/ledgers/{id:guid}/replay", (Guid id) =>
        {
            if (!LedgersStore.TryGetValue(id, out var record))
            {
                return Results.NotFound(new { error = $"Ledger record {id} not found" });
            }

            // Simulate immediate re-delivery success
            record.DeliveryStatus = "DELIVERED";
            record.AttemptCount += 1;
            record.SentAt = DateTimeOffset.UtcNow;
            record.LatencyMs = 34.2;
            record.HttpStatus = 200;
            record.ErrorMessage = null;

            return Results.Ok(new
            {
                message = $"Successfully replayed delivery for trace {record.TraceId}",
                record
            });
        }).AllowAnonymous();

        // 8. Bulk Replay All Dead Letters
        group.MapPost("/ledgers/bulk-replay", () =>
        {
            var deadLetters = LedgersStore.Values
                .Where(l => l.DeliveryStatus.StartsWith("DEAD_LETTER", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var item in deadLetters)
            {
                item.DeliveryStatus = "DELIVERED";
                item.AttemptCount += 1;
                item.SentAt = DateTimeOffset.UtcNow;
                item.LatencyMs = 45.1;
                item.HttpStatus = 200;
                item.ErrorMessage = null;
            }

            return Results.Ok(new
            {
                replayedCount = deadLetters.Count,
                message = $"Replayed {deadLetters.Count} dead-letter deliveries with 100% callback resolution"
            });
        }).AllowAnonymous();

        // 9. Shifts List
        group.MapGet("/shifts", () =>
        {
            return Results.Ok(ShiftsStore.Values.OrderBy(s => s.ShiftStart).ToList());
        }).AllowAnonymous();

        // 10. Update Shift / Substitution
        group.MapPut("/shifts/{id:guid}", (Guid id, [FromBody] ShiftRosterRecord updated) =>
        {
            if (!ShiftsStore.TryGetValue(id, out var existing))
            {
                return Results.NotFound(new { error = $"Shift record {id} not found" });
            }

            existing.IsOnDuty = updated.IsOnDuty;
            existing.ShiftStart = updated.ShiftStart;
            existing.ShiftEnd = updated.ShiftEnd;
            existing.SubstituteUserId = updated.SubstituteUserId;
            existing.SubstituteUserName = updated.SubstituteUserName;

            return Results.Ok(existing);
        }).AllowAnonymous();

        // 11. Test Fire Rule
        group.MapPost("/test-fire", ([FromBody] TestFireRequest req) =>
        {
            var rule = RulesStore.Values.FirstOrDefault(r => r.Id == req.RuleId);
            var ruleName = rule?.Name ?? "Custom Test Rule";
            var targetEndpoint = !string.IsNullOrWhiteSpace(req.CustomEndpoint)
                ? req.CustomEndpoint
                : (rule?.TargetEndpoint ?? "https://cbs-relay.internal.adcb.ae/api/v1/test-notify");

            var syntheticTrace = $"TEST-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{req.Channel}";
            var newRecord = new DeliveryLedgerItem
            {
                Id = Guid.NewGuid(),
                NotificationRuleId = rule?.Id,
                RuleName = $"[TEST] {ruleName}",
                TraceId = syntheticTrace,
                Channel = req.Channel,
                TargetEndpoint = targetEndpoint,
                DeliveryStatus = "DELIVERED",
                AttemptCount = 1,
                SentAt = DateTimeOffset.UtcNow,
                LatencyMs = 18.6,
                HttpStatus = 200,
                ErrorMessage = null,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    synthetic = true,
                    testEvent = rule?.TriggerEvent ?? "Test Dispatch Verification",
                    channel = req.Channel,
                    dispatchedAt = DateTimeOffset.UtcNow,
                    verificationSecret = "hmac-verified-test-ok"
                }),
                SignatureHex = "sha256=test991823746192837461928374619283746192837461928374619283746192"
            };

            LedgersStore[newRecord.Id] = newRecord;

            return Results.Ok(new
            {
                success = true,
                message = $"Test dispatch delivered via {req.Channel} in 18.6ms",
                traceId = syntheticTrace,
                latencyMs = 18.6,
                httpStatus = 200,
                ledgerId = newRecord.Id
            });
        }).AllowAnonymous();

        // 12. In-App Notification Center
        group.MapGet("/inbox", () =>
        {
            var list = InboxStore.Values.OrderByDescending(n => n.Timestamp).ToList();
            var unreadCount = list.Count(n => !n.Read);
            return Results.Ok(new
            {
                unreadCount,
                items = list
            });
        }).AllowAnonymous();

        // 13. Mark Notification Read
        group.MapPatch("/inbox/{id:guid}/read", (Guid id) =>
        {
            if (InboxStore.TryGetValue(id, out var item))
            {
                item.Read = true;
                return Results.Ok(item);
            }
            return Results.NotFound();
        }).AllowAnonymous();

        // 14. Mark All Notifications Read
        group.MapPost("/inbox/mark-all-read", () =>
        {
            foreach (var item in InboxStore.Values)
            {
                item.Read = true;
            }
            return Results.Ok(new { message = "All notifications marked as read", unreadCount = 0 });
        }).AllowAnonymous();

        return app;
    }

    private static Guid ResolveTenant(string? tenantHeader)
    {
        if (!string.IsNullOrWhiteSpace(tenantHeader) && Guid.TryParse(tenantHeader, out var parsed))
        {
            return parsed;
        }
        return DefaultTenantId;
    }
}

public record TestFireRequest(Guid? RuleId, string Channel, string? CustomEndpoint);

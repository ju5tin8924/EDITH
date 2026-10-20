using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Edith.Events.Contracts;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Edith.Events.Webhooks;

public interface IWebhookDeliveryLedger
{
    Task RecordDeliveryAsync(WebhookDeliveryRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookDeliveryRecord>> GetDeadLettersAsync(CancellationToken ct = default);
}

public sealed class InMemoryWebhookDeliveryLedger : IWebhookDeliveryLedger
{
    private readonly ConcurrentQueue<WebhookDeliveryRecord> _records = new();

    public Task RecordDeliveryAsync(WebhookDeliveryRecord record, CancellationToken ct = default)
    {
        _records.Enqueue(record);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WebhookDeliveryRecord>> GetDeadLettersAsync(CancellationToken ct = default)
    {
        var list = _records.Where(r => r.DeliveryStatus.StartsWith("DEAD_LETTER", StringComparison.OrdinalIgnoreCase)).ToList();
        return Task.FromResult<IReadOnlyList<WebhookDeliveryRecord>>(list);
    }
}

public sealed class PostgreSqlWebhookDeliveryLedger : IWebhookDeliveryLedger
{
    private readonly string _connectionString;
    private readonly InMemoryWebhookDeliveryLedger _fallback = new();

    public PostgreSqlWebhookDeliveryLedger(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task RecordDeliveryAsync(WebhookDeliveryRecord record, CancellationToken ct = default)
    {
        await _fallback.RecordDeliveryAsync(record, ct);
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = @"
                INSERT INTO notifications.delivery_ledgers (id, notification_rule_id, trace_id, delivery_status, sent_at)
                VALUES (@id, @ruleId, @traceId, @status, @sentAt);";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", record.Id);
            cmd.Parameters.AddWithValue("ruleId", (object?)record.NotificationRuleId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("traceId", record.TraceId.Length > 100 ? record.TraceId[..100] : record.TraceId);
            cmd.Parameters.AddWithValue("status", record.DeliveryStatus.Length > 50 ? record.DeliveryStatus[..50] : record.DeliveryStatus);
            cmd.Parameters.AddWithValue("sentAt", record.SentAt);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (NpgsqlException)
        {
            // Stored in in-memory fallback when PostgreSQL is offline
        }
    }

    public async Task<IReadOnlyList<WebhookDeliveryRecord>> GetDeadLettersAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = @"
                SELECT id, notification_rule_id, trace_id, delivery_status, sent_at
                FROM notifications.delivery_ledgers
                WHERE delivery_status LIKE 'DEAD_LETTER%'
                ORDER BY sent_at DESC
                LIMIT 100;";

            var list = new List<WebhookDeliveryRecord>();
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new WebhookDeliveryRecord
                {
                    Id = r.GetGuid(0),
                    NotificationRuleId = r.IsDBNull(1) ? null : r.GetGuid(1),
                    TraceId = r.GetString(2),
                    DeliveryStatus = r.GetString(3),
                    SentAt = r.GetFieldValue<DateTimeOffset>(4)
                });
            }
            return list;
        }
        catch (NpgsqlException)
        {
            return await _fallback.GetDeadLettersAsync(ct);
        }
    }
}

public interface ICbsWebhookDispatcher
{
    Task<CbsWebhookResult> DispatchDecisionWebhookAsync(
        AmlDecisionWebhookPayload payload,
        string cbsWebhookUrl,
        string sharedSecret,
        CancellationToken ct = default);
}

public sealed partial class CbsWebhookDispatcher : ICbsWebhookDispatcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CbsWebhookDispatcher> _logger;
    private readonly IWebhookDeliveryLedger? _deliveryLedger;
    private readonly int _maxAttempts;
    private readonly int _baseDelayMs;

    public CbsWebhookDispatcher(
        HttpClient httpClient,
        ILogger<CbsWebhookDispatcher> logger,
        IWebhookDeliveryLedger? deliveryLedger = null,
        int maxAttempts = 3,
        int baseDelayMs = 50)
    {
        _httpClient = httpClient;
        _logger = logger;
        _deliveryLedger = deliveryLedger;
        _maxAttempts = Math.Max(1, maxAttempts);
        _baseDelayMs = Math.Max(1, baseDelayMs);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "CBS webhook attempt {Attempt}/{MaxAttempts} rejected with status {StatusCode}: {Body}")]
    private static partial void LogWebhookWarning(ILogger logger, int attempt, int maxAttempts, System.Net.HttpStatusCode statusCode, string body);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to deliver CBS webhook for transaction {TxRef} on attempt {Attempt}/{MaxAttempts}")]
    private static partial void LogWebhookError(ILogger logger, Exception ex, string txRef, int attempt, int maxAttempts);

    public async Task<CbsWebhookResult> DispatchDecisionWebhookAsync(
        AmlDecisionWebhookPayload payload,
        string cbsWebhookUrl,
        string sharedSecret,
        CancellationToken ct = default)
    {
        var jsonPayload = JsonSerializer.Serialize(payload);
        var signatureHex = HmacSignatureHelper.ComputeSignature(jsonPayload, sharedSecret);
        var traceId = $"{payload.TransactionRefNo}:{payload.Decision}";

        int lastStatusCode = 0;
        string? lastResponseBody = null;
        string? lastError = null;

        for (int attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, cbsWebhookUrl)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("X-Edith-Signature", $"sha256={signatureHex}");
                request.Headers.Add("X-Edith-Timestamp", payload.Timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
                request.Headers.Add("X-Edith-Event", payload.EventType);
                request.Headers.Add("X-Tenant-ID", payload.TenantId.ToString());
                request.Headers.Add("X-Edith-Attempt", attempt.ToString(CultureInfo.InvariantCulture));

                using var response = await _httpClient.SendAsync(request, ct);
                lastStatusCode = (int)response.StatusCode;
                lastResponseBody = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    if (_deliveryLedger != null)
                    {
                        await _deliveryLedger.RecordDeliveryAsync(new WebhookDeliveryRecord
                        {
                            TraceId = traceId,
                            DeliveryStatus = "DELIVERED",
                            SentAt = DateTimeOffset.UtcNow
                        }, ct);
                    }

                    return new CbsWebhookResult
                    {
                        Success = true,
                        StatusCode = lastStatusCode,
                        AttemptCount = attempt,
                        ResponsePayload = lastResponseBody
                    };
                }

                LogWebhookWarning(_logger, attempt, _maxAttempts, response.StatusCode, lastResponseBody);
                lastError = $"CBS webhook responded with HTTP {response.StatusCode}";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogWebhookError(_logger, ex, payload.TransactionRefNo, attempt, _maxAttempts);
                lastStatusCode = 0;
                lastError = ex.Message;
            }

            if (attempt < _maxAttempts)
            {
                var delayMs = _baseDelayMs * (1 << (attempt - 1));
                await Task.Delay(delayMs, ct);
            }
        }

        // Exhausted retries -> store in dead-letter delivery ledger (notifications.delivery_ledgers)
        bool deadLetterLogged = false;
        if (_deliveryLedger != null)
        {
            await _deliveryLedger.RecordDeliveryAsync(new WebhookDeliveryRecord
            {
                TraceId = traceId,
                DeliveryStatus = $"DEAD_LETTER_HTTP_{lastStatusCode}",
                SentAt = DateTimeOffset.UtcNow
            }, ct);
            deadLetterLogged = true;
        }

        return new CbsWebhookResult
        {
            Success = false,
            StatusCode = lastStatusCode,
            AttemptCount = _maxAttempts,
            DeadLetterLogged = deadLetterLogged,
            ResponsePayload = lastResponseBody,
            ErrorMessage = lastError
        };
    }
}

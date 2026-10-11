using System.Globalization;
using System.Text;
using System.Text.Json;
using Edith.Events.Contracts;
using Microsoft.Extensions.Logging;

namespace Edith.Events.Webhooks;

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

    public CbsWebhookDispatcher(HttpClient httpClient, ILogger<CbsWebhookDispatcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "CBS webhook rejected with status {StatusCode}: {Body}")]
    private static partial void LogWebhookWarning(ILogger logger, System.Net.HttpStatusCode statusCode, string body);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to deliver CBS webhook for transaction {TxRef}")]
    private static partial void LogWebhookError(ILogger logger, Exception ex, string txRef);

    public async Task<CbsWebhookResult> DispatchDecisionWebhookAsync(
        AmlDecisionWebhookPayload payload,
        string cbsWebhookUrl,
        string sharedSecret,
        CancellationToken ct = default)
    {
        try
        {
            var jsonPayload = JsonSerializer.Serialize(payload);
            var signatureHex = HmacSignatureHelper.ComputeSignature(jsonPayload, sharedSecret);

            using var request = new HttpRequestMessage(HttpMethod.Post, cbsWebhookUrl)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("X-Edith-Signature", $"sha256={signatureHex}");
            request.Headers.Add("X-Edith-Timestamp", payload.Timestamp.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            request.Headers.Add("X-Edith-Event", payload.EventType);
            request.Headers.Add("X-Tenant-ID", payload.TenantId.ToString());

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return new CbsWebhookResult
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    ResponsePayload = responseBody
                };
            }

            LogWebhookWarning(_logger, response.StatusCode, responseBody);
            return new CbsWebhookResult
            {
                Success = false,
                StatusCode = (int)response.StatusCode,
                ResponsePayload = responseBody,
                ErrorMessage = $"CBS webhook responded with HTTP {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            LogWebhookError(_logger, ex, payload.TransactionRefNo);
            return new CbsWebhookResult
            {
                Success = false,
                StatusCode = 0,
                ErrorMessage = ex.Message
            };
        }
    }
}

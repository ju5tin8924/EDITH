using System.Text.Json;
using Edith.Events.Contracts;
using Edith.Events.Kafka;
using Microsoft.Extensions.Logging;

namespace Edith.Events.Outbox;

public interface IOutboxProcessor
{
    Task<int> ProcessPendingOutboxMessagesAsync(int batchSize = 100, CancellationToken ct = default);
}

public sealed partial class OutboxProcessor : IOutboxProcessor
{
    private readonly IOutboxRepository _outboxRepo;
    private readonly IEventBus _eventBus;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IOutboxRepository outboxRepo,
        IEventBus eventBus,
        ILogger<OutboxProcessor> logger)
    {
        _outboxRepo = outboxRepo;
        _eventBus = eventBus;
        _logger = logger;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to dispatch outbox message {MessageId} of type {EventType}")]
    private static partial void LogDispatchError(ILogger logger, Exception ex, Guid messageId, string eventType);

    public async Task<int> ProcessPendingOutboxMessagesAsync(int batchSize = 100, CancellationToken ct = default)
    {
        var pending = await _outboxRepo.FetchPendingMessagesAsync(batchSize, ct);
        var processedCount = 0;

        foreach (var message in pending)
        {
            try
            {
                var topic = ResolveTopicForEventType(message.EventType);
                var partitionKey = message.TenantId.ToString();

                if (message.EventType == nameof(TransactionHeldEvent))
                {
                    var evt = JsonSerializer.Deserialize<TransactionHeldEvent>(message.Payload);
                    if (evt != null)
                    {
                        await _eventBus.PublishAsync(topic, partitionKey, evt, ct);
                    }
                }
                else
                {
                    // Generic payload dispatch
                    var doc = JsonSerializer.Deserialize<JsonDocument>(message.Payload);
                    if (doc != null)
                    {
                        await _eventBus.PublishAsync(topic, partitionKey, doc, ct);
                    }
                }

                await _outboxRepo.MarkProcessedAsync(message.Id, ct);
                processedCount++;
            }
            catch (Exception ex)
            {
                LogDispatchError(_logger, ex, message.Id, message.EventType);
                await _outboxRepo.MarkFailedAsync(message.Id, ex.Message, ct);
            }
        }

        return processedCount;
    }

    private static string ResolveTopicForEventType(string eventType)
    {
        return eventType switch
        {
            nameof(TransactionHeldEvent) => EdithTopics.TransactionHeld,
            "TransactionClearedEvent" => EdithTopics.TransactionCleared,
            _ => EdithTopics.AuditEvents
        };
    }
}

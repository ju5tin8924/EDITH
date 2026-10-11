using System.Collections.Concurrent;
using System.Text.Json;

namespace Edith.Events.Kafka;

public static class EdithTopics
{
    public const string TransactionHeld = "edith.tx.held.v1";
    public const string TransactionCleared = "edith.tx.cleared.v1";
    public const string CaseEvents = "edith.case.events.v1";
    public const string BulkScreening = "edith.screening.bulk.v1";
    public const string AuditEvents = "edith.audit.events.v1";
}

public interface IEventBus
{
    Task PublishAsync<T>(string topic, string partitionKey, T message, CancellationToken ct = default) where T : class;
    void Subscribe<T>(string topic, Func<T, CancellationToken, Task> handler) where T : class;
}

public sealed class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<string, List<Func<string, CancellationToken, Task>>> _handlers = new();

    public async Task PublishAsync<T>(string topic, string partitionKey, T message, CancellationToken ct = default) where T : class
    {
        var payloadJson = JsonSerializer.Serialize(message);
        if (_handlers.TryGetValue(topic, out var topicHandlers))
        {
            var runningTasks = topicHandlers.Select(h => h(payloadJson, ct));
            await Task.WhenAll(runningTasks);
        }
    }

    public void Subscribe<T>(string topic, Func<T, CancellationToken, Task> handler) where T : class
    {
        _handlers.AddOrUpdate(
            topic,
            _ => [WrapHandler(handler)],
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(WrapHandler(handler));
                }
                return list;
            });
    }

    private static Func<string, CancellationToken, Task> WrapHandler<T>(Func<T, CancellationToken, Task> typedHandler) where T : class
    {
        return async (json, ct) =>
        {
            var obj = JsonSerializer.Deserialize<T>(json);
            if (obj != null)
            {
                await typedHandler(obj, ct);
            }
        };
    }
}

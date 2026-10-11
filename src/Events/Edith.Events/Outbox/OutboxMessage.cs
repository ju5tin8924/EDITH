using System.Collections.Concurrent;

namespace Edith.Events.Outbox;

public sealed record OutboxMessage
{
    public required Guid Id { get; init; } = Guid.NewGuid();
    public required Guid TenantId { get; init; }
    public required string EventType { get; init; }
    public required Guid AggregateId { get; init; }
    public required string Payload { get; init; }
    public string Status { get; set; } = "PENDING"; // PENDING | PROCESSED | FAILED
    public int RetryCount { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}

public interface IOutboxRepository
{
    Task AppendMessageAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxMessage>> FetchPendingMessagesAsync(int batchSize = 100, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task MarkFailedAsync(Guid messageId, string errorMessage, CancellationToken ct = default);
}

public sealed class InMemoryOutboxRepository : IOutboxRepository
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();

    public Task AppendMessageAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _messages[message.Id] = message;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> FetchPendingMessagesAsync(int batchSize = 100, CancellationToken ct = default)
    {
        var pending = _messages.Values
            .Where(m => m.Status == "PENDING")
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<OutboxMessage>>(pending);
    }

    public Task MarkProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            msg.Status = "PROCESSED";
            msg.ProcessedAt = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(Guid messageId, string errorMessage, CancellationToken ct = default)
    {
        if (_messages.TryGetValue(messageId, out var msg))
        {
            msg.Status = "FAILED";
            msg.LastError = errorMessage;
            msg.RetryCount++;
        }

        return Task.CompletedTask;
    }
}

using Edith.Screening.Bulk.Models;

namespace Edith.Screening.Bulk.Notifications;

public interface IBulkProgressNotifier
{
    Task NotifyProgressAsync(BulkProgressUpdate update, CancellationToken ct = default);
}

/// <summary>
/// Progress notifier hook bridging background bulk workers to SignalR hubs, webhooks, or in-memory observers.
/// </summary>
public sealed class CallbackProgressNotifier : IBulkProgressNotifier
{
    private readonly Action<BulkProgressUpdate>? _callback;

    public CallbackProgressNotifier(Action<BulkProgressUpdate>? callback = null)
    {
        _callback = callback;
    }

    public Task NotifyProgressAsync(BulkProgressUpdate update, CancellationToken ct = default)
    {
        _callback?.Invoke(update);
        return Task.CompletedTask;
    }
}

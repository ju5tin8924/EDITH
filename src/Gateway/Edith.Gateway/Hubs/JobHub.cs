using Microsoft.AspNetCore.SignalR;

namespace Edith.Gateway.Hubs;

public interface IJobClient
{
    Task JobProgressUpdated(string jobId, int processed, int total, int hitCount, double percent);
    Task JobCompleted(string jobId, int totalHits);
}

public sealed class JobHub : Hub<IJobClient>
{
    public async Task JoinJobGroup(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
    }

    public async Task LeaveJobGroup(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job-{jobId}");
    }
}

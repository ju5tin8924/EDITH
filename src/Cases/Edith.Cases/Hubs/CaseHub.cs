using Microsoft.AspNetCore.SignalR;

namespace Edith.Cases.Hubs;

public interface ICaseClient
{
    Task CaseLocked(Guid caseId, Guid lockedBy, string lockedByName, DateTimeOffset lockedAt);
    Task CaseUnlocked(Guid caseId);
    Task CaseStatusChanged(Guid caseId, string newStatus, string? resolution, string actorName);
    Task CaseNoteAdded(Guid caseId, Guid noteId, string authorName, string content);
}

public sealed class CaseHub : Hub<ICaseClient>
{
    public async Task JoinTenantGroup(string tenantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
    }

    public async Task LeaveTenantGroup(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
    }
}

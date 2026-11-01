using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Edith.Cases.Hubs;

public interface ICaseClient
{
    Task CaseLocked(Guid caseId, Guid lockedBy, string lockedByName, DateTimeOffset lockedAt);
    Task CaseUnlocked(Guid caseId);
    Task CaseStatusChanged(Guid caseId, string newStatus, string? resolution, string actorName);
    Task CaseNoteAdded(Guid caseId, Guid noteId, string authorName, string content);
}

[Authorize]
public sealed class CaseHub : Hub<ICaseClient>
{
    public async Task JoinTenantGroup(string tenantId)
    {
        var claimTenant = Context.User?.FindFirst("tenant_id")?.Value
                       ?? Context.User?.FindFirst("tid")?.Value;

        // Prevent joining another tenant's real-time SignalR event group
        if (!string.IsNullOrWhiteSpace(claimTenant) &&
            !string.Equals(claimTenant, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new HubException("Cross-tenant subscription denied.");
        }

        var effectiveTenant = claimTenant ?? tenantId;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{effectiveTenant}");
    }

    public async Task LeaveTenantGroup(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
    }
}

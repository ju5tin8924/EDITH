using System.Collections.Concurrent;
using Edith.Screening.Core.Models;

namespace Edith.Screening.Api.Services;

public interface IWhitelistService
{
    Task<IReadOnlyList<WhitelistEntry>> GetActiveWhitelistsAsync(Guid tenantId, CancellationToken ct = default);

    Task<WhitelistEntry> AddWhitelistAsync(WhitelistEntry entry, CancellationToken ct = default);

    Task<bool> RemoveWhitelistAsync(Guid tenantId, Guid whitelistId, CancellationToken ct = default);
}

public sealed class InMemoryWhitelistService : IWhitelistService
{
    private readonly ConcurrentDictionary<Guid, WhitelistEntry> _entries = new();

    public Task<IReadOnlyList<WhitelistEntry>> GetActiveWhitelistsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var active = _entries.Values
            .Where(w => w.TenantId == tenantId && w.IsActive && (w.ExpiresAt == null || w.ExpiresAt > now))
            .ToList();

        return Task.FromResult<IReadOnlyList<WhitelistEntry>>(active);
    }

    public Task<WhitelistEntry> AddWhitelistAsync(WhitelistEntry entry, CancellationToken ct = default)
    {
        _entries[entry.Id] = entry;
        return Task.FromResult(entry);
    }

    public Task<bool> RemoveWhitelistAsync(Guid tenantId, Guid whitelistId, CancellationToken ct = default)
    {
        if (_entries.TryGetValue(whitelistId, out var entry) && entry.TenantId == tenantId)
        {
            return Task.FromResult(_entries.TryRemove(whitelistId, out _));
        }

        return Task.FromResult(false);
    }
}

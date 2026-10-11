using System.Collections.Concurrent;
using Edith.Cases.Models;

namespace Edith.Cases.Locking;

public interface ICaseLockManager
{
    Task<CaseLockResult> AcquireLockAsync(Guid tenantId, Guid caseId, Guid userId, string userName, TimeSpan? duration = null, CancellationToken ct = default);
    Task<bool> ReleaseLockAsync(Guid tenantId, Guid caseId, Guid userId, bool force = false, CancellationToken ct = default);
    Task<CaseLockResult?> GetLockAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    void EnsureLockHeldByUser(CaseDetail caseDetail, Guid userId);
}

public sealed class CaseLockManager : ICaseLockManager
{
    private sealed record LockEntry(Guid TenantId, Guid CaseId, Guid UserId, string UserName, DateTimeOffset LockedAt, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<Guid, LockEntry> _locks = new();
    private static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(15);

    public Task<CaseLockResult> AcquireLockAsync(
        Guid tenantId,
        Guid caseId,
        Guid userId,
        string userName,
        TimeSpan? duration = null,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var lockDuration = duration ?? DefaultLockDuration;
        var expiresAt = now.Add(lockDuration);

        while (true)
        {
            if (_locks.TryGetValue(caseId, out var existing))
            {
                // Lock held by the same user -> refresh
                if (existing.UserId == userId)
                {
                    var refreshed = existing with { ExpiresAt = expiresAt };
                    _locks[caseId] = refreshed;
                    return Task.FromResult(CaseLockResult.Success(userId, userName, refreshed.LockedAt, expiresAt));
                }

                // Lock held by another user and not expired
                if (existing.ExpiresAt > now)
                {
                    return Task.FromResult(CaseLockResult.Contended(existing.UserId, existing.UserName, existing.LockedAt, existing.ExpiresAt));
                }

                // Lock held by another user but expired -> overwrite
                var takeover = new LockEntry(tenantId, caseId, userId, userName, now, expiresAt);
                if (_locks.TryUpdate(caseId, takeover, existing))
                {
                    return Task.FromResult(CaseLockResult.Success(userId, userName, now, expiresAt));
                }

                continue;
            }

            // No existing lock
            var newLock = new LockEntry(tenantId, caseId, userId, userName, now, expiresAt);
            if (_locks.TryAdd(caseId, newLock))
            {
                return Task.FromResult(CaseLockResult.Success(userId, userName, now, expiresAt));
            }
        }
    }

    public Task<bool> ReleaseLockAsync(
        Guid tenantId,
        Guid caseId,
        Guid userId,
        bool force = false,
        CancellationToken ct = default)
    {
        if (_locks.TryGetValue(caseId, out var existing))
        {
            if (force || existing.UserId == userId)
            {
                return Task.FromResult(_locks.TryRemove(caseId, out _));
            }
        }

        return Task.FromResult(false);
    }

    public Task<CaseLockResult?> GetLockAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        if (_locks.TryGetValue(caseId, out var existing))
        {
            if (existing.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return Task.FromResult<CaseLockResult?>(new CaseLockResult
                {
                    IsLocked = true,
                    LockedBy = existing.UserId,
                    LockedByName = existing.UserName,
                    LockedAt = existing.LockedAt,
                    ExpiresAt = existing.ExpiresAt
                });
            }

            _locks.TryRemove(caseId, out _);
        }

        return Task.FromResult<CaseLockResult?>(null);
    }

    public void EnsureLockHeldByUser(CaseDetail caseDetail, Guid userId)
    {
        if (_locks.TryGetValue(caseDetail.Id, out var existing) && existing.ExpiresAt > DateTimeOffset.UtcNow)
        {
            if (existing.UserId != userId)
            {
                throw new CaseLockedByAnotherUserException(existing.UserId, existing.UserName);
            }
        }
    }
}

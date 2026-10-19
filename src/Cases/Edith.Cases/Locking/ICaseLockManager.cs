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

public sealed class PostgreSqlCaseLockManager : ICaseLockManager
{
    private readonly string _connectionString;
    private static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(15);

    public PostgreSqlCaseLockManager(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<CaseLockResult> AcquireLockAsync(
        Guid tenantId,
        Guid caseId,
        Guid userId,
        string userName,
        TimeSpan? duration = null,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(duration ?? DefaultLockDuration);

        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string updateSql = """
            UPDATE cases.aml_cases
            SET locked_by_user_id = @user_id,
                locked_by_name = @user_name,
                locked_at = @locked_at,
                lock_expires_at = @expires_at,
                updated_at = @locked_at
            WHERE id = @id AND tenant_id = @tenant_id
              AND (locked_by_user_id IS NULL OR locked_by_user_id = @user_id OR lock_expires_at IS NULL OR lock_expires_at <= @locked_at);
            """;

        await using (var cmd = new Npgsql.NpgsqlCommand(updateSql, conn))
        {
            cmd.Parameters.AddWithValue("id", caseId);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("user_name", userName);
            cmd.Parameters.AddWithValue("locked_at", now);
            cmd.Parameters.AddWithValue("expires_at", expiresAt);

            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows > 0)
            {
                return CaseLockResult.Success(userId, userName, now, expiresAt);
            }
        }

        var existing = await GetLockAsync(tenantId, caseId, ct);
        if (existing != null && existing.LockedBy.HasValue)
        {
            return CaseLockResult.Contended(
                existing.LockedBy.Value,
                existing.LockedByName ?? "Unknown",
                existing.LockedAt ?? now,
                existing.ExpiresAt ?? expiresAt);
        }

        return CaseLockResult.Success(userId, userName, now, expiresAt);
    }

    public async Task<bool> ReleaseLockAsync(
        Guid tenantId,
        Guid caseId,
        Guid userId,
        bool force = false,
        CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            UPDATE cases.aml_cases
            SET locked_by_user_id = NULL,
                locked_by_name = NULL,
                locked_at = NULL,
                lock_expires_at = NULL,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id AND tenant_id = @tenant_id
              AND (@force = true OR locked_by_user_id = @user_id);
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", caseId);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("user_id", userId);
        cmd.Parameters.AddWithValue("force", force);

        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<CaseLockResult?> GetLockAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT locked_by_user_id, locked_by_name, locked_at, lock_expires_at
            FROM cases.aml_cases
            WHERE id = @id AND tenant_id = @tenant_id;
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", caseId);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct) && !reader.IsDBNull(0) && !reader.IsDBNull(3))
        {
            var expiresAt = reader.GetFieldValue<DateTimeOffset>(3);
            if (expiresAt > DateTimeOffset.UtcNow)
            {
                return new CaseLockResult
                {
                    IsLocked = true,
                    LockedBy = reader.GetGuid(0),
                    LockedByName = reader.IsDBNull(1) ? "Analyst" : reader.GetString(1),
                    LockedAt = reader.IsDBNull(2) ? DateTimeOffset.UtcNow : reader.GetFieldValue<DateTimeOffset>(2),
                    ExpiresAt = expiresAt
                };
            }
        }

        return null;
    }

    public void EnsureLockHeldByUser(CaseDetail caseDetail, Guid userId)
    {
        if (caseDetail.LockedBy.HasValue &&
            caseDetail.LockExpiresAt.HasValue &&
            caseDetail.LockExpiresAt.Value > DateTimeOffset.UtcNow &&
            caseDetail.LockedBy.Value != userId)
        {
            throw new CaseLockedByAnotherUserException(caseDetail.LockedBy.Value, caseDetail.LockedByName ?? "Another Analyst");
        }
    }
}

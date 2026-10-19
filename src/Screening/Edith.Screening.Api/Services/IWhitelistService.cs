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

public sealed class PostgreSqlWhitelistService : IWhitelistService
{
    private readonly string _connectionString;

    public PostgreSqlWhitelistService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<WhitelistEntry>> GetActiveWhitelistsAsync(Guid tenantId, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT id, tenant_id, customer_id, entity_identifier, match_scope, reason, expires_at, is_active
            FROM screening.whitelists
            WHERE tenant_id = @tenant_id AND is_active = true AND (expires_at IS NULL OR expires_at > CURRENT_TIMESTAMP);
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        var list = new List<WhitelistEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new WhitelistEntry
            {
                Id = reader.GetGuid(0),
                TenantId = reader.GetGuid(1),
                CustomerId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                EntityIdentifier = reader.GetString(3),
                MatchScope = reader.GetString(4),
                Reason = reader.GetString(5),
                ExpiresAt = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
                IsActive = reader.GetBoolean(7)
            });
        }

        return list;
    }

    public async Task<WhitelistEntry> AddWhitelistAsync(WhitelistEntry entry, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            INSERT INTO screening.whitelists (id, tenant_id, customer_id, entity_identifier, match_scope, reason, expires_at, is_active)
            VALUES (@id, @tenant_id, @customer_id, @entity_identifier, @match_scope, @reason, @expires_at, @is_active)
            ON CONFLICT (id) DO UPDATE SET
                entity_identifier = EXCLUDED.entity_identifier,
                match_scope = EXCLUDED.match_scope,
                reason = EXCLUDED.reason,
                expires_at = EXCLUDED.expires_at,
                is_active = EXCLUDED.is_active;
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entry.Id);
        cmd.Parameters.AddWithValue("tenant_id", entry.TenantId);
        cmd.Parameters.AddWithValue("customer_id", (object?)entry.CustomerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("entity_identifier", entry.EntityIdentifier);
        cmd.Parameters.AddWithValue("match_scope", entry.MatchScope);
        cmd.Parameters.AddWithValue("reason", entry.Reason);
        cmd.Parameters.AddWithValue("expires_at", (object?)entry.ExpiresAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("is_active", entry.IsActive);

        await cmd.ExecuteNonQueryAsync(ct);
        return entry;
    }

    public async Task<bool> RemoveWhitelistAsync(Guid tenantId, Guid whitelistId, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = new Npgsql.NpgsqlCommand(
            "DELETE FROM screening.whitelists WHERE id = @id AND tenant_id = @tenant_id;", conn);
        cmd.Parameters.AddWithValue("id", whitelistId);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }
}

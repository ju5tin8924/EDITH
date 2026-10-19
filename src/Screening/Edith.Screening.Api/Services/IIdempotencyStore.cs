using System.Collections.Concurrent;
using Edith.Screening.Api.Contracts;

namespace Edith.Screening.Api.Services;

public interface IIdempotencyStore
{
    Task<ScreenTransactionResponse?> GetCachedDecisionAsync(Guid tenantId, string idempotencyKey, CancellationToken ct = default);

    Task SaveDecisionAsync(Guid tenantId, string idempotencyKey, ScreenTransactionResponse response, TimeSpan? ttl = null, CancellationToken ct = default);
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private sealed record CacheEntry(ScreenTransactionResponse Response, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheEntry> _store = new(StringComparer.Ordinal);
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(24);

    public Task<ScreenTransactionResponse?> GetCachedDecisionAsync(Guid tenantId, string idempotencyKey, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, idempotencyKey);
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                // Return copy marked as replayed
                var replayed = entry.Response with { IdempotencyReplayed = true };
                return Task.FromResult<ScreenTransactionResponse?>(replayed);
            }

            _store.TryRemove(key, out _);
        }

        return Task.FromResult<ScreenTransactionResponse?>(null);
    }

    public Task SaveDecisionAsync(Guid tenantId, string idempotencyKey, ScreenTransactionResponse response, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var key = BuildKey(tenantId, idempotencyKey);
        var expiration = DateTimeOffset.UtcNow.Add(ttl ?? _defaultTtl);
        _store[key] = new CacheEntry(response, expiration);
        return Task.CompletedTask;
    }

    private static string BuildKey(Guid tenantId, string idempotencyKey) => $"{tenantId:D}:{idempotencyKey}";
}

public sealed class PostgreSqlIdempotencyStore : IIdempotencyStore
{
    private readonly string _connectionString;
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(24);
    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public PostgreSqlIdempotencyStore(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ScreenTransactionResponse?> GetCachedDecisionAsync(Guid tenantId, string idempotencyKey, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT response_json
            FROM screening.idempotency_cache
            WHERE tenant_id = @tenant_id AND idempotency_key = @key AND expires_at > CURRENT_TIMESTAMP;
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("key", idempotencyKey);

        var json = await cmd.ExecuteScalarAsync(ct) as string;
        if (!string.IsNullOrWhiteSpace(json))
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<ScreenTransactionResponse>(json, JsonOpts);
            if (parsed != null)
            {
                return parsed with { IdempotencyReplayed = true };
            }
        }

        return null;
    }

    public async Task SaveDecisionAsync(Guid tenantId, string idempotencyKey, ScreenTransactionResponse response, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var expiresAt = DateTimeOffset.UtcNow.Add(ttl ?? _defaultTtl);
        var json = System.Text.Json.JsonSerializer.Serialize(response, JsonOpts);

        const string sql = """
            INSERT INTO screening.idempotency_cache (tenant_id, idempotency_key, response_json, expires_at)
            VALUES (@tenant_id, @key, @json::jsonb, @expires_at)
            ON CONFLICT (tenant_id, idempotency_key) DO UPDATE SET
                response_json = EXCLUDED.response_json,
                expires_at = EXCLUDED.expires_at;
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("key", idempotencyKey);
        cmd.Parameters.AddWithValue("json", json);
        cmd.Parameters.AddWithValue("expires_at", expiresAt);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}

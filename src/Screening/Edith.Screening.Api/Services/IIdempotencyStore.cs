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

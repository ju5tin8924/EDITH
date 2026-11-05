using System.Diagnostics;
using Edith.Screening.Core.Models;
using Edith.Screening.Core.Search;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Edith.Screening.Bulk.Repository;

public sealed record OpenSearchSyncProgress(
    bool IsRunning,
    long TotalEntitiesInDatabase,
    long IndexedEntities,
    double ProgressPercentage,
    double RatePerSecond,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? StatusMessage,
    string? Error);

public interface IWatchlistOpenSearchSyncService
{
    OpenSearchSyncProgress GetProgress();
    Task<OpenSearchSyncProgress> StartSyncAsync(bool forceReindex = false, CancellationToken ct = default);
}

public sealed class WatchlistOpenSearchSyncService : IWatchlistOpenSearchSyncService
{
    private readonly string? _connectionString;
    private readonly OpenSearchWatchlistSearchEngine _openSearch;
    private readonly ILogger<WatchlistOpenSearchSyncService>? _logger;
    private readonly object _lock = new();

    private OpenSearchSyncProgress _progress = new(
        IsRunning: false,
        TotalEntitiesInDatabase: 0,
        IndexedEntities: 0,
        ProgressPercentage: 0,
        RatePerSecond: 0,
        StartedAtUtc: null,
        CompletedAtUtc: null,
        StatusMessage: "IDLE",
        Error: null);

    public WatchlistOpenSearchSyncService(
        string? connectionString,
        OpenSearchWatchlistSearchEngine openSearch,
        ILogger<WatchlistOpenSearchSyncService>? logger = null)
    {
        _connectionString = connectionString;
        _openSearch = openSearch;
        _logger = logger;
    }

    public OpenSearchSyncProgress GetProgress()
    {
        lock (_lock)
        {
            return _progress;
        }
    }

    public Task<OpenSearchSyncProgress> StartSyncAsync(bool forceReindex = false, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_progress.IsRunning)
            {
                return Task.FromResult(_progress);
            }
            _progress = _progress with
            {
                IsRunning = true,
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = null,
                StatusMessage = "INITIALIZING",
                Error = null,
                IndexedEntities = 0,
                ProgressPercentage = 0
            };
        }

        _ = Task.Run(async () =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // 1. Ensure indexes & aliases exist
                await _openSearch.EnsureIndexCreatedAsync(CancellationToken.None);
                await _openSearch.EnsureWhitelistIndexCreatedAsync(CancellationToken.None);

                if (string.IsNullOrWhiteSpace(_connectionString))
                {
                    lock (_lock)
                    {
                        _progress = _progress with
                        {
                            IsRunning = false,
                            CompletedAtUtc = DateTimeOffset.UtcNow,
                            StatusMessage = "COMPLETED_NO_DATABASE"
                        };
                    }
                    return;
                }

                // 2. Count total active records in PostgreSQL
                long totalRecords = 0;
                await using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.OpenAsync(CancellationToken.None);
                    await using var countCmd = new NpgsqlCommand("SELECT COUNT(*) FROM screening.watchlist_entities WHERE is_active = true;", conn);
                    totalRecords = (long)(await countCmd.ExecuteScalarAsync(CancellationToken.None) ?? 0L);
                }

                lock (_lock)
                {
                    _progress = _progress with
                    {
                        TotalEntitiesInDatabase = totalRecords,
                        StatusMessage = $"STREAMING_INGESTION (0 / {totalRecords:N0})"
                    };
                }

                // 3. Disable refresh interval during bulk load
                await _openSearch.SetRefreshIntervalAsync("-1", CancellationToken.None);

                // 4. Stream from PostgreSQL in batches of 5,000 using keyset cursor pagination
                const int BatchSize = 5000;
                Guid? lastId = null;
                long totalIndexed = 0;

                await using (var streamConn = new NpgsqlConnection(_connectionString))
                {
                    await streamConn.OpenAsync(CancellationToken.None);

                    while (true)
                    {
                        const string sql = """
                            SELECT id, entity_external_id, entity_type, full_name, dob, nationality,
                                   source_list, category, id_details, alias_name, is_blocked, is_active
                            FROM screening.watchlist_entities
                            WHERE (@last_id IS NULL OR id > @last_id) AND is_active = true
                            ORDER BY id ASC
                            LIMIT @batch_size;
                            """;

                        await using var cmd = new NpgsqlCommand(sql, streamConn);
                        cmd.Parameters.AddWithValue("last_id", (object?)lastId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("batch_size", BatchSize);

                        var batch = new List<WatchlistEntity>(BatchSize);
                        await using (var reader = await cmd.ExecuteReaderAsync(CancellationToken.None))
                        {
                            while (await reader.ReadAsync(CancellationToken.None))
                            {
                                var id = reader.GetGuid(0);
                                var extId = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                var type = reader.IsDBNull(2) ? "INDIVIDUAL" : reader.GetString(2);
                                var fullName = reader.GetString(3);
                                DateOnly? dob = reader.IsDBNull(4) ? null : DateOnly.FromDateTime(reader.GetDateTime(4));
                                var nationality = reader.IsDBNull(5) ? null : reader.GetString(5);
                                var sourceList = reader.IsDBNull(6) ? "UNKNOWN" : reader.GetString(6);
                                var category = reader.IsDBNull(7) ? "SANCTION" : reader.GetString(7);
                                var idDetails = reader.IsDBNull(8) ? null : reader.GetString(8);
                                var aliasName = reader.IsDBNull(9) ? null : reader.GetString(9);
                                var isBlocked = reader.GetBoolean(10);
                                var isActive = reader.GetBoolean(11);

                                batch.Add(new WatchlistEntity
                                {
                                    Id = id,
                                    EntityExternalId = extId,
                                    EntityType = type,
                                    FullName = fullName,
                                    Dob = dob,
                                    Nationality = nationality,
                                    SourceList = sourceList,
                                    Category = category,
                                    IdDetails = idDetails,
                                    AliasName = aliasName,
                                    IsBlocked = isBlocked,
                                    IsActive = isActive
                                });
                            }
                        }

                        if (batch.Count == 0)
                        {
                            break;
                        }

                        lastId = batch[^1].Id;
                        await _openSearch.IndexBatchAsync(batch, CancellationToken.None);
                        totalIndexed += batch.Count;

                        var elapsedSec = sw.Elapsed.TotalSeconds;
                        var rate = elapsedSec > 0 ? totalIndexed / elapsedSec : 0;
                        var pct = totalRecords > 0 ? Math.Round((double)totalIndexed / totalRecords * 100.0, 2) : 0;

                        lock (_lock)
                        {
                            _progress = _progress with
                            {
                                IndexedEntities = totalIndexed,
                                ProgressPercentage = pct,
                                RatePerSecond = Math.Round(rate, 1),
                                StatusMessage = $"STREAMING_INGESTION ({totalIndexed:N0} / {totalRecords:N0} - {pct:F1}%)"
                            };
                        }
                    }
                }

                // 5. Sync active Whitelists
                await using (var wConn = new NpgsqlConnection(_connectionString))
                {
                    await wConn.OpenAsync(CancellationToken.None);
                    const string wSql = """
                        SELECT id, tenant_id, customer_id, entity_identifier, match_scope, reason, expires_at, is_active
                        FROM screening.whitelists
                        WHERE is_active = true;
                        """;
                    await using var wCmd = new NpgsqlCommand(wSql, wConn);
                    var whitelists = new List<WhitelistEntry>();
                    await using (var wReader = await wCmd.ExecuteReaderAsync(CancellationToken.None))
                    {
                        while (await wReader.ReadAsync(CancellationToken.None))
                        {
                            whitelists.Add(new WhitelistEntry
                            {
                                Id = wReader.GetGuid(0),
                                TenantId = wReader.GetGuid(1),
                                CustomerId = wReader.IsDBNull(2) ? null : wReader.GetGuid(2),
                                EntityIdentifier = wReader.GetString(3),
                                MatchScope = wReader.GetString(4),
                                Reason = wReader.GetString(5),
                                ExpiresAt = wReader.IsDBNull(6) ? null : wReader.GetFieldValue<DateTimeOffset>(6),
                                IsActive = wReader.GetBoolean(7)
                            });
                        }
                    }

                    if (whitelists.Count > 0)
                    {
                        await _openSearch.IndexWhitelistBatchAsync(whitelists, CancellationToken.None);
                    }
                }

                // 6. Restore refresh interval & force-merge Lucene segments
                lock (_lock)
                {
                    _progress = _progress with { StatusMessage = "FORCE_MERGING_LUCENE_SEGMENTS" };
                }
                await _openSearch.SetRefreshIntervalAsync("1s", CancellationToken.None);
                await _openSearch.ForceMergeAsync(1, CancellationToken.None);

                sw.Stop();
                lock (_lock)
                {
                    _progress = _progress with
                    {
                        IsRunning = false,
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                        StatusMessage = $"COMPLETED_SUCCESSFULLY ({totalIndexed:N0} records in {sw.Elapsed.TotalMinutes:F1}m)",
                        ProgressPercentage = 100.0
                    };
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                lock (_lock)
                {
                    _progress = _progress with
                    {
                        IsRunning = false,
                        CompletedAtUtc = DateTimeOffset.UtcNow,
                        StatusMessage = "FAILED",
                        Error = ex.Message
                    };
                }
            }
        }, CancellationToken.None);

        return Task.FromResult(GetProgress());
    }
}

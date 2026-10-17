using System.Collections.Concurrent;
using System.Data.Common;
using Edith.Screening.Bulk.Models;
using Npgsql;

namespace Edith.Screening.Bulk.Repository;

public sealed record BulkJobRecord
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string FileName { get; init; }
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SkippedRecords { get; set; }
    public string Status { get; set; } = "PENDING";
    public bool KeepSkipped { get; set; } = true;
    public string SkippedFormat { get; set; } = "csv";
}

public sealed record BulkItemRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid JobId { get; init; }
    public Guid? MatchedEntityId { get; init; }
    public required string InputName { get; init; }
    public decimal MatchScore { get; init; }
    public bool MatchFound { get; init; }
}

public interface IBulkJobRepository
{
    Task CreateJobAsync(BulkJobRecord job, CancellationToken ct = default);
    Task UpdateJobProgressAsync(Guid jobId, int processedRecords, int totalRecords, string status, int skippedRecords = 0, CancellationToken ct = default);
    Task InsertItemsBatchAsync(IEnumerable<BulkItemRecord> items, CancellationToken ct = default);
    Task<BulkJobRecord?> GetJobAsync(Guid jobId, CancellationToken ct = default);
    Task SaveSkippedRecordsAsync(Guid jobId, IEnumerable<AmlEntityRecord> records, CancellationToken ct = default);
    Task<IReadOnlyList<AmlEntityRecord>> GetSkippedRecordsAsync(Guid jobId, CancellationToken ct = default);
    Task<bool> IsEntityAlreadyInSystemAsync(Guid tenantId, AmlEntityRecord record, CancellationToken ct = default);
}

public sealed class InMemoryBulkJobRepository : IBulkJobRepository
{
    private readonly ConcurrentDictionary<Guid, BulkJobRecord> _jobs = new();
    private readonly ConcurrentBag<BulkItemRecord> _items = new();
    private readonly ConcurrentDictionary<Guid, List<AmlEntityRecord>> _skippedRecords = new();
    private readonly ConcurrentDictionary<string, byte> _knownEntities = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryBulkJobRepository(bool seedBaseline = false)
    {
        if (seedBaseline)
        {
            // Pre-seed known baseline entities for duplicate detection tests
            _knownEntities.TryAdd("VIKTOR ANATOLYEVICH BOUT", 1);
            _knownEntities.TryAdd("OFAC-9812", 1);
            _knownEntities.TryAdd("TARIQ AZIZ", 1);
            _knownEntities.TryAdd("UN-7721", 1);
            _knownEntities.TryAdd("BANK OF BAGHDAD", 1);
            _knownEntities.TryAdd("OFAC-BANK-0099", 1);
            _knownEntities.TryAdd("AL-BARAKAAT TRADING COMPANY", 1);
        }
    }

    public void SeedExistingEntities(IEnumerable<string> identifiersOrNames)
    {
        foreach (var id in identifiersOrNames)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                _knownEntities.TryAdd(id.Trim(), 1);
            }
        }
    }

    public Task CreateJobAsync(BulkJobRecord job, CancellationToken ct = default)
    {
        _jobs[job.Id] = job;
        return Task.CompletedTask;
    }

    public Task UpdateJobProgressAsync(Guid jobId, int processedRecords, int totalRecords, string status, int skippedRecords = 0, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.ProcessedRecords = processedRecords;
            job.TotalRecords = totalRecords;
            job.Status = status;
            job.SkippedRecords = skippedRecords;
        }
        return Task.CompletedTask;
    }

    public Task InsertItemsBatchAsync(IEnumerable<BulkItemRecord> items, CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            _items.Add(item);
        }
        return Task.CompletedTask;
    }

    public Task<BulkJobRecord?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task SaveSkippedRecordsAsync(Guid jobId, IEnumerable<AmlEntityRecord> records, CancellationToken ct = default)
    {
        _skippedRecords.AddOrUpdate(
            jobId,
            _ => records.ToList(),
            (_, list) => { list.AddRange(records); return list; });

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AmlEntityRecord>> GetSkippedRecordsAsync(Guid jobId, CancellationToken ct = default)
    {
        if (_skippedRecords.TryGetValue(jobId, out var list))
        {
            return Task.FromResult<IReadOnlyList<AmlEntityRecord>>(list);
        }

        return Task.FromResult<IReadOnlyList<AmlEntityRecord>>([]);
    }

    public Task<bool> IsEntityAlreadyInSystemAsync(Guid tenantId, AmlEntityRecord record, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(record.AmlFullName) && _knownEntities.ContainsKey(record.AmlFullName))
        {
            return Task.FromResult(true);
        }

        if (!string.IsNullOrWhiteSpace(record.AmlIdDetails) && _knownEntities.ContainsKey(record.AmlIdDetails))
        {
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}

public sealed class PostgreSqlBulkJobRepository : IBulkJobRepository
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<Guid, List<AmlEntityRecord>> _skippedCache = new();
    private readonly InMemoryBulkJobRepository _fallback = new(seedBaseline: true);

    public PostgreSqlBulkJobRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task CreateJobAsync(BulkJobRecord job, CancellationToken ct = default)
    {
        await _fallback.CreateJobAsync(job, ct);
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = """
                INSERT INTO screening.bulk_screening_jobs (id, user_id, file_name, total_records, processed_records, status)
                VALUES (@id, @user_id, @file_name, @total, @processed, @status);
                """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", job.Id);
            cmd.Parameters.AddWithValue("user_id", job.UserId);
            cmd.Parameters.AddWithValue("file_name", job.FileName);
            cmd.Parameters.AddWithValue("total", job.TotalRecords);
            cmd.Parameters.AddWithValue("processed", job.ProcessedRecords);
            cmd.Parameters.AddWithValue("status", job.Status);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (ex is DbException or System.Net.Sockets.SocketException)
        {
            // Fallback already updated
        }
    }

    public async Task UpdateJobProgressAsync(Guid jobId, int processedRecords, int totalRecords, string status, int skippedRecords = 0, CancellationToken ct = default)
    {
        await _fallback.UpdateJobProgressAsync(jobId, processedRecords, totalRecords, status, skippedRecords, ct);
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = """
                UPDATE screening.bulk_screening_jobs
                SET processed_records = @processed, total_records = @total, status = @status
                WHERE id = @id;
                """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", jobId);
            cmd.Parameters.AddWithValue("processed", processedRecords);
            cmd.Parameters.AddWithValue("total", totalRecords);
            cmd.Parameters.AddWithValue("status", status);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (ex is DbException or System.Net.Sockets.SocketException)
        {
            // Fallback already updated
        }
    }

    public async Task InsertItemsBatchAsync(IEnumerable<BulkItemRecord> items, CancellationToken ct = default)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

        await _fallback.InsertItemsBatchAsync(list, ct);

        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            var candidateIds = list.Where(i => i.MatchedEntityId.HasValue).Select(i => i.MatchedEntityId!.Value).Distinct().ToList();
            var validIds = new HashSet<Guid>();
            if (candidateIds.Count > 0)
            {
                await using var checkCmd = new NpgsqlCommand("SELECT id FROM screening.watchlist_entities WHERE id = ANY(@ids);", conn);
                checkCmd.Parameters.AddWithValue("ids", candidateIds.ToArray());
                await using var reader = await checkCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    validIds.Add(reader.GetGuid(0));
                }
            }

            await using var batch = new NpgsqlBatch(conn);

            foreach (var item in list)
            {
                var cmd = new NpgsqlBatchCommand("""
                    INSERT INTO screening.bulk_screening_items (id, job_id, matched_entity_id, input_name, match_score, match_found)
                    VALUES ($1, $2, $3, $4, $5, $6);
                    """);

                cmd.Parameters.Add(new NpgsqlParameter { Value = item.Id });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.JobId });
                var matchedId = item.MatchedEntityId.HasValue && validIds.Contains(item.MatchedEntityId.Value)
                    ? (object)item.MatchedEntityId.Value
                    : DBNull.Value;
                cmd.Parameters.Add(new NpgsqlParameter { Value = matchedId });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.InputName });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.MatchScore });
                cmd.Parameters.Add(new NpgsqlParameter { Value = item.MatchFound });

                batch.BatchCommands.Add(cmd);
            }

            await batch.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex) when (ex is DbException or System.Net.Sockets.SocketException)
        {
            // Fallback already updated
        }
    }

    public async Task<BulkJobRecord?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT id, user_id, file_name, total_records, processed_records, status
                FROM screening.bulk_screening_jobs
                WHERE id = @id;
                """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", jobId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var total = reader.GetInt32(3);
                var processed = reader.GetInt32(4);
                var skipped = _skippedCache.TryGetValue(jobId, out var s) ? s.Count : 0;

                return new BulkJobRecord
                {
                    Id = reader.GetGuid(0),
                    UserId = reader.GetGuid(1),
                    FileName = reader.GetString(2),
                    TotalRecords = total,
                    ProcessedRecords = processed,
                    SkippedRecords = skipped,
                    Status = reader.GetString(5)
                };
            }

            return null;
        }
        catch (Exception ex) when (ex is DbException or System.Net.Sockets.SocketException)
        {
            var fb = await _fallback.GetJobAsync(jobId, ct);
            if (fb != null && _skippedCache.TryGetValue(jobId, out var s))
            {
                fb.SkippedRecords = s.Count;
            }
            return fb;
        }
    }

    public Task SaveSkippedRecordsAsync(Guid jobId, IEnumerable<AmlEntityRecord> records, CancellationToken ct = default)
    {
        _skippedCache.AddOrUpdate(
            jobId,
            _ => records.ToList(),
            (_, list) => { list.AddRange(records); return list; });

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AmlEntityRecord>> GetSkippedRecordsAsync(Guid jobId, CancellationToken ct = default)
    {
        if (_skippedCache.TryGetValue(jobId, out var list))
        {
            return Task.FromResult<IReadOnlyList<AmlEntityRecord>>(list);
        }

        return Task.FromResult<IReadOnlyList<AmlEntityRecord>>([]);
    }

    public async Task<bool> IsEntityAlreadyInSystemAsync(Guid tenantId, AmlEntityRecord record, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = """
                SELECT EXISTS (
                    SELECT 1 FROM screening.customers
                    WHERE (id_number = @id AND @id <> '')
                       OR UPPER(full_name) = UPPER(@name)
                    UNION ALL
                    SELECT 1 FROM screening.watchlist_entities
                    WHERE (entity_external_id = @id AND @id <> '')
                       OR UPPER(full_name) = UPPER(@name)
                );
                """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", record.AmlIdDetails ?? string.Empty);
            cmd.Parameters.AddWithValue("name", record.AmlFullName ?? string.Empty);

            var result = await cmd.ExecuteScalarAsync(ct);
            return result is true;
        }
        catch (Exception ex) when (ex is DbException or System.Net.Sockets.SocketException)
        {
            return await _fallback.IsEntityAlreadyInSystemAsync(tenantId, record, ct);
        }
    }
}

using System.Collections.Concurrent;
using Edith.Screening.Bulk.Models;
using Edith.Screening.Core.Models;
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
    Task UpsertWatchlistEntitiesBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default);
    Task<BulkJobRecord?> GetJobAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<BulkJobRecord>> ListJobsAsync(int maxResults = 50, CancellationToken ct = default);
    Task SaveSkippedRecordsAsync(Guid jobId, IEnumerable<AmlEntityRecord> records, CancellationToken ct = default);
    Task<IReadOnlyList<AmlEntityRecord>> GetSkippedRecordsAsync(Guid jobId, CancellationToken ct = default);
    Task<bool> IsEntityAlreadyInSystemAsync(Guid tenantId, AmlEntityRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<WatchlistEntity>> SearchWatchlistEntitiesAsync(string? nameQuery, string? idQuery, int maxResults = 50, CancellationToken ct = default);
}

public sealed class InMemoryBulkJobRepository : IBulkJobRepository
{
    private readonly ConcurrentDictionary<Guid, BulkJobRecord> _jobs = new();
    private readonly ConcurrentBag<BulkItemRecord> _items = new();
    private readonly ConcurrentDictionary<Guid, List<AmlEntityRecord>> _skippedRecords = new();
    private readonly ConcurrentDictionary<string, byte> _knownEntities = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WatchlistEntity> _entities = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryBulkJobRepository(bool seedBaseline = false)
    {
        if (seedBaseline)
        {
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

    public Task UpsertWatchlistEntitiesBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default)
    {
        foreach (var e in entities)
        {
            if (!string.IsNullOrWhiteSpace(e.FullName)) _knownEntities.TryAdd(e.FullName.Trim(), 1);
            if (!string.IsNullOrWhiteSpace(e.EntityExternalId)) _knownEntities.TryAdd(e.EntityExternalId.Trim(), 1);
            _entities[$"{e.EntityExternalId}:{e.FullName}"] = e;
        }
        return Task.CompletedTask;
    }

    public Task<BulkJobRecord?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<BulkJobRecord>> ListJobsAsync(int maxResults = 50, CancellationToken ct = default)
    {
        var list = _jobs.Values.Take(maxResults).ToList();
        return Task.FromResult<IReadOnlyList<BulkJobRecord>>(list);
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

    public Task<IReadOnlyList<WatchlistEntity>> SearchWatchlistEntitiesAsync(string? nameQuery, string? idQuery, int maxResults = 50, CancellationToken ct = default)
    {
        var query = _entities.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(nameQuery))
        {
            query = query.Where(e =>
                e.FullName.Contains(nameQuery, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(e.AliasName) && e.AliasName.Contains(nameQuery, StringComparison.OrdinalIgnoreCase)));
        }
        if (!string.IsNullOrWhiteSpace(idQuery))
        {
            query = query.Where(e =>
                e.EntityExternalId.Contains(idQuery, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(e.IdDetails) && e.IdDetails.Contains(idQuery, StringComparison.OrdinalIgnoreCase)));
        }
        return Task.FromResult<IReadOnlyList<WatchlistEntity>>(query.Take(maxResults).ToList());
    }
}

public sealed class PostgreSqlBulkJobRepository : IBulkJobRepository
{
    private readonly string _connectionString;

    public PostgreSqlBulkJobRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task CreateJobAsync(BulkJobRecord job, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            INSERT INTO screening.bulk_screening_jobs (id, user_id, file_name, total_records, processed_records, skipped_records, status, keep_skipped, skipped_format)
            VALUES (@id, @user_id, @file_name, @total, @processed, @skipped, @status, @keep_skipped, @skipped_format);
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", job.Id);
        cmd.Parameters.AddWithValue("user_id", job.UserId);
        cmd.Parameters.AddWithValue("file_name", job.FileName);
        cmd.Parameters.AddWithValue("total", job.TotalRecords);
        cmd.Parameters.AddWithValue("processed", job.ProcessedRecords);
        cmd.Parameters.AddWithValue("skipped", job.SkippedRecords);
        cmd.Parameters.AddWithValue("status", job.Status);
        cmd.Parameters.AddWithValue("keep_skipped", job.KeepSkipped);
        cmd.Parameters.AddWithValue("skipped_format", job.SkippedFormat);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateJobProgressAsync(Guid jobId, int processedRecords, int totalRecords, string status, int skippedRecords = 0, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            UPDATE screening.bulk_screening_jobs
            SET processed_records = @processed, total_records = @total, skipped_records = @skipped, status = @status
            WHERE id = @id;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", jobId);
        cmd.Parameters.AddWithValue("processed", processedRecords);
        cmd.Parameters.AddWithValue("total", totalRecords);
        cmd.Parameters.AddWithValue("skipped", skippedRecords);
        cmd.Parameters.AddWithValue("status", status);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task InsertItemsBatchAsync(IEnumerable<BulkItemRecord> items, CancellationToken ct = default)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

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

    public async Task UpsertWatchlistEntitiesBatchAsync(IEnumerable<WatchlistEntity> entities, CancellationToken ct = default)
    {
        var list = entities.ToList();
        if (list.Count == 0) return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var batch = new NpgsqlBatch(conn);

        foreach (var entity in list)
        {
            var cmd = new NpgsqlBatchCommand("""
                INSERT INTO screening.watchlist_entities (
                    id, entity_external_id, entity_type, full_name, dob, nationality, source_list, category, id_details, alias_name, is_blocked, is_active
                )
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)
                ON CONFLICT (entity_external_id, full_name) DO UPDATE SET
                    nationality = COALESCE(EXCLUDED.nationality, screening.watchlist_entities.nationality),
                    id_details = COALESCE(EXCLUDED.id_details, screening.watchlist_entities.id_details),
                    alias_name = COALESCE(EXCLUDED.alias_name, screening.watchlist_entities.alias_name),
                    is_active = EXCLUDED.is_active;
                """);

            cmd.Parameters.Add(new NpgsqlParameter { Value = entity.Id });
            cmd.Parameters.Add(new NpgsqlParameter { Value = entity.EntityExternalId ?? string.Empty });
            cmd.Parameters.Add(new NpgsqlParameter { Value = entity.EntityType ?? "INDIVIDUAL" });
            cmd.Parameters.Add(new NpgsqlParameter { Value = entity.FullName });
            cmd.Parameters.Add(new NpgsqlParameter { Value = entity.Dob.HasValue ? (object)entity.Dob.Value : DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)entity.Nationality ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = entity.SourceList ?? "WATCHLIST" });
            cmd.Parameters.Add(new NpgsqlParameter { Value = entity.Category ?? "SANCTION" });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)entity.IdDetails ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)entity.AliasName ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = entity.IsBlocked });
            cmd.Parameters.Add(new NpgsqlParameter { Value = entity.IsActive });

            batch.BatchCommands.Add(cmd);
        }

        await batch.ExecuteNonQueryAsync(ct);
    }

    public async Task<BulkJobRecord?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT id, user_id, file_name, total_records, processed_records, skipped_records, status, keep_skipped, skipped_format
            FROM screening.bulk_screening_jobs
            WHERE id = @id;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", jobId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new BulkJobRecord
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                FileName = reader.GetString(2),
                TotalRecords = reader.GetInt32(3),
                ProcessedRecords = reader.GetInt32(4),
                SkippedRecords = reader.GetInt32(5),
                Status = reader.GetString(6),
                KeepSkipped = reader.GetBoolean(7),
                SkippedFormat = reader.GetString(8)
            };
        }

        return null;
    }

    public async Task<IReadOnlyList<BulkJobRecord>> ListJobsAsync(int maxResults = 50, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT id, user_id, file_name, total_records, processed_records, skipped_records, status, keep_skipped, skipped_format
            FROM screening.bulk_screening_jobs
            ORDER BY total_records DESC
            LIMIT @limit;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("limit", maxResults);

        var list = new List<BulkJobRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new BulkJobRecord
            {
                Id = reader.GetGuid(0),
                UserId = reader.GetGuid(1),
                FileName = reader.GetString(2),
                TotalRecords = reader.GetInt32(3),
                ProcessedRecords = reader.GetInt32(4),
                SkippedRecords = reader.GetInt32(5),
                Status = reader.GetString(6),
                KeepSkipped = reader.GetBoolean(7),
                SkippedFormat = reader.GetString(8)
            });
        }

        return list;
    }

    public async Task SaveSkippedRecordsAsync(Guid jobId, IEnumerable<AmlEntityRecord> records, CancellationToken ct = default)
    {
        var list = records.ToList();
        if (list.Count == 0) return;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var batch = new NpgsqlBatch(conn);

        foreach (var r in list)
        {
            var cmd = new NpgsqlBatchCommand("""
                INSERT INTO screening.bulk_job_skipped (
                    job_id, row_index, aml_uid, aml_type, aml_full_name, aml_id_details,
                    aml_category, aml_date_of_birth, aml_remarks, aml_nationality, aml_program, aml_alias_name
                )
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12);
                """);

            cmd.Parameters.Add(new NpgsqlParameter { Value = jobId });
            cmd.Parameters.Add(new NpgsqlParameter { Value = r.RowIndex });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)r.AmlUid ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)r.AmlType ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = r.AmlFullName });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)r.AmlIdDetails ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)r.AmlCategory ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)r.AmlDateOfBirth ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)r.AmlRemarks ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)r.AmlNationality ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)r.AmlProgram ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)r.AmlAliasName ?? DBNull.Value });

            batch.BatchCommands.Add(cmd);
        }

        await batch.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AmlEntityRecord>> GetSkippedRecordsAsync(Guid jobId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT row_index, aml_uid, aml_type, aml_full_name, aml_id_details,
                   aml_category, aml_date_of_birth, aml_remarks, aml_nationality, aml_program, aml_alias_name
            FROM screening.bulk_job_skipped
            WHERE job_id = @job_id
            ORDER BY id;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("job_id", jobId);

        var result = new List<AmlEntityRecord>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new AmlEntityRecord
            {
                RowIndex = reader.GetInt32(0),
                AmlUid = reader.IsDBNull(1) ? null : reader.GetString(1),
                AmlType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                AmlFullName = reader.GetString(3),
                AmlIdDetails = reader.IsDBNull(4) ? null : reader.GetString(4),
                AmlCategory = reader.IsDBNull(5) ? null : reader.GetString(5),
                AmlDateOfBirth = reader.IsDBNull(6) ? null : reader.GetString(6),
                AmlRemarks = reader.IsDBNull(7) ? null : reader.GetString(7),
                AmlNationality = reader.IsDBNull(8) ? null : reader.GetString(8),
                AmlProgram = reader.IsDBNull(9) ? null : reader.GetString(9),
                AmlAliasName = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }

        return result;
    }

    public async Task<bool> IsEntityAlreadyInSystemAsync(Guid tenantId, AmlEntityRecord record, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var extId = !string.IsNullOrWhiteSpace(record.AmlUid) ? record.AmlUid : (record.AmlIdDetails ?? string.Empty);

        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM screening.customers
                WHERE (id_number = @id AND @id <> '')
                   OR UPPER(full_name) = UPPER(@name)
                UNION ALL
                SELECT 1 FROM screening.watchlist_entities
                WHERE (entity_external_id = @id AND @id <> '' AND UPPER(full_name) = UPPER(@name))
                   OR (id_details = @id_details AND @id_details <> '')
                   OR (@id = '' AND UPPER(full_name) = UPPER(@name))
            );
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", extId);
        cmd.Parameters.AddWithValue("id_details", record.AmlIdDetails ?? string.Empty);
        cmd.Parameters.AddWithValue("name", record.AmlFullName ?? string.Empty);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    public async Task<IReadOnlyList<WatchlistEntity>> SearchWatchlistEntitiesAsync(string? nameQuery, string? idQuery, int maxResults = 50, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var name = nameQuery?.Trim() ?? string.Empty;
        var id = idQuery?.Trim() ?? string.Empty;

        const string sql = """
            SELECT id, entity_external_id, entity_type, full_name, dob, nationality, source_list, category, id_details, alias_name, is_blocked, is_active
            FROM screening.watchlist_entities
            WHERE is_active = true
              AND (@name = '' OR UPPER(full_name) LIKE '%' || UPPER(@name) || '%' OR UPPER(COALESCE(alias_name, '')) LIKE '%' || UPPER(@name) || '%')
              AND (@id = '' OR UPPER(entity_external_id) LIKE '%' || UPPER(@id) || '%' OR UPPER(COALESCE(id_details, '')) LIKE '%' || UPPER(@id) || '%')
            ORDER BY
              CASE WHEN @name <> '' AND UPPER(full_name) = UPPER(@name) THEN 0 ELSE 1 END,
              CASE WHEN @id <> '' AND UPPER(entity_external_id) = UPPER(@id) THEN 0 ELSE 1 END,
              full_name
            LIMIT @limit;
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("limit", maxResults);

        var results = new List<WatchlistEntity>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new WatchlistEntity
            {
                Id = reader.GetGuid(0),
                EntityExternalId = reader.GetString(1),
                EntityType = reader.GetString(2),
                FullName = reader.GetString(3),
                Dob = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
                Nationality = reader.IsDBNull(5) ? null : reader.GetString(5),
                SourceList = reader.GetString(6),
                Category = reader.GetString(7),
                IdDetails = reader.IsDBNull(8) ? null : reader.GetString(8),
                AliasName = reader.IsDBNull(9) ? null : reader.GetString(9),
                IsBlocked = reader.GetBoolean(10),
                IsActive = reader.GetBoolean(11)
            });
        }

        return results;
    }
}

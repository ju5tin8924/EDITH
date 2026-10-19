using System.Collections.Concurrent;
using Edith.Cases.Hubs;
using Edith.Cases.Locking;
using Edith.Cases.Models;
using Edith.Cases.StateMachine;
using Microsoft.AspNetCore.SignalR;

namespace Edith.Cases.Services;

public interface ICaseService
{
    Task<CaseDetail> CreateCaseAsync(CaseDetail caseDetail, CancellationToken ct = default);
    Task<CaseDetail?> GetCaseByIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<IReadOnlyList<CaseDetail>> ListCasesAsync(Guid tenantId, CaseStatus? statusFilter = null, CancellationToken ct = default);
    Task<CaseLockResult> AcquireLockAsync(Guid tenantId, Guid caseId, Guid userId, string userName, TimeSpan? duration = null, CancellationToken ct = default);
    Task<bool> ReleaseLockAsync(Guid tenantId, Guid caseId, Guid userId, bool force = false, CancellationToken ct = default);
    Task<CaseDetail> TransitionStatusAsync(Guid tenantId, Guid caseId, CaseStatus newStatus, Guid userId, string userName, string userRole, CaseResolution? resolution = null, string? reason = null, CancellationToken ct = default);
    Task<CaseNote> AddNoteAsync(Guid tenantId, Guid caseId, Guid authorId, string authorName, string content, string noteType = "INVESTIGATION", CancellationToken ct = default);
    Task<IReadOnlyList<CaseNote>> GetNotesAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task<IReadOnlyList<CaseAuditEntry>> GetAuditTrailAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
}

public sealed class CaseService : ICaseService
{
    private readonly ConcurrentDictionary<Guid, CaseDetail> _cases = new();
    private readonly ConcurrentDictionary<Guid, List<CaseNote>> _notes = new();
    private readonly ConcurrentDictionary<Guid, List<CaseAuditEntry>> _auditTrails = new();
    private readonly ICaseLockManager _lockManager;
    private readonly IHubContext<CaseHub, ICaseClient>? _hubContext;

    public CaseService(
        ICaseLockManager lockManager,
        IHubContext<CaseHub, ICaseClient>? hubContext = null)
    {
        _lockManager = lockManager;
        _hubContext = hubContext;
    }

    public Task<CaseDetail> CreateCaseAsync(CaseDetail caseDetail, CancellationToken ct = default)
    {
        _cases[caseDetail.Id] = caseDetail;
        RecordAudit(caseDetail.Id, "CREATED", "NONE", caseDetail.Status.ToString(), caseDetail.TenantId, "SYSTEM", "Case initialized from screening event");
        return Task.FromResult(caseDetail);
    }

    public Task<CaseDetail?> GetCaseByIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        if (_cases.TryGetValue(caseId, out var detail) && detail.TenantId == tenantId)
        {
            return Task.FromResult<CaseDetail?>(detail);
        }

        return Task.FromResult<CaseDetail?>(null);
    }

    public Task<IReadOnlyList<CaseDetail>> ListCasesAsync(Guid tenantId, CaseStatus? statusFilter = null, CancellationToken ct = default)
    {
        var list = _cases.Values
            .Where(c => c.TenantId == tenantId && (!statusFilter.HasValue || c.Status == statusFilter.Value))
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<CaseDetail>>(list);
    }

    public async Task<CaseLockResult> AcquireLockAsync(
        Guid tenantId,
        Guid caseId,
        Guid userId,
        string userName,
        TimeSpan? duration = null,
        CancellationToken ct = default)
    {
        var currentCase = await GetCaseOrThrowAsync(tenantId, caseId);
        var result = await _lockManager.AcquireLockAsync(tenantId, caseId, userId, userName, duration, ct);

        if (result.IsLocked)
        {
            currentCase.LockedBy = userId;
            currentCase.LockedByName = userName;
            currentCase.LockedAt = result.LockedAt;
            currentCase.LockExpiresAt = result.ExpiresAt;

            RecordAudit(caseId, "LOCKED", currentCase.Status.ToString(), currentCase.Status.ToString(), userId, userName, "Lock acquired for investigation");

            if (_hubContext != null && result.LockedAt.HasValue)
            {
                await _hubContext.Clients.Group($"tenant-{tenantId}")
                    .CaseLocked(caseId, userId, userName, result.LockedAt.Value);
            }
        }

        return result;
    }

    public async Task<bool> ReleaseLockAsync(
        Guid tenantId,
        Guid caseId,
        Guid userId,
        bool force = false,
        CancellationToken ct = default)
    {
        var currentCase = await GetCaseOrThrowAsync(tenantId, caseId);
        var released = await _lockManager.ReleaseLockAsync(tenantId, caseId, userId, force, ct);

        if (released)
        {
            currentCase.LockedBy = null;
            currentCase.LockedByName = null;
            currentCase.LockedAt = null;
            currentCase.LockExpiresAt = null;

            RecordAudit(caseId, "UNLOCKED", currentCase.Status.ToString(), currentCase.Status.ToString(), userId, "SYSTEM", force ? "Lock force-released" : "Lock released");

            if (_hubContext != null)
            {
                await _hubContext.Clients.Group($"tenant-{tenantId}").CaseUnlocked(caseId);
            }
        }

        return released;
    }

    public async Task<CaseDetail> TransitionStatusAsync(
        Guid tenantId,
        Guid caseId,
        CaseStatus newStatus,
        Guid userId,
        string userName,
        string userRole,
        CaseResolution? resolution = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var currentCase = await GetCaseOrThrowAsync(tenantId, caseId);

        // 1. Ensure user holds lock if case is locked
        _lockManager.EnsureLockHeldByUser(currentCase, userId);

        // 2. Validate state machine transition & RBAC
        CaseStateMachine.ValidateTransition(currentCase.Status, newStatus, userRole, resolution);

        var prevStatus = currentCase.Status;
        currentCase.Status = newStatus;
        currentCase.UpdatedAt = DateTimeOffset.UtcNow;

        if (resolution.HasValue)
        {
            currentCase.Resolution = resolution.Value;
            currentCase.ResolutionReason = reason;
            currentCase.ResolvedAt = DateTimeOffset.UtcNow;
            currentCase.ResolvedBy = userId;
        }

        if (newStatus is CaseStatus.PendingApproval or CaseStatus.Resolved or CaseStatus.Closed)
        {
            await _lockManager.ReleaseLockAsync(tenantId, caseId, userId, force: true, ct);
            currentCase.LockedBy = null;
            currentCase.LockedByName = null;
            currentCase.LockedAt = null;
            currentCase.LockExpiresAt = null;
        }

        RecordAudit(caseId, "STATUS_CHANGE", prevStatus.ToString(), newStatus.ToString(), userId, userName, reason);

        if (_hubContext != null)
        {
            await _hubContext.Clients.Group($"tenant-{tenantId}")
                .CaseStatusChanged(caseId, newStatus.ToString(), resolution?.ToString(), userName);
        }

        return currentCase;
    }

    public async Task<CaseNote> AddNoteAsync(
        Guid tenantId,
        Guid caseId,
        Guid authorId,
        string authorName,
        string content,
        string noteType = "INVESTIGATION",
        CancellationToken ct = default)
    {
        var currentCase = await GetCaseOrThrowAsync(tenantId, caseId);

        var note = new CaseNote
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            AuthorId = authorId,
            AuthorName = authorName,
            Content = content,
            NoteType = noteType
        };

        var list = _notes.GetOrAdd(caseId, _ => []);
        lock (list)
        {
            list.Add(note);
        }

        RecordAudit(caseId, "NOTE_ADDED", currentCase.Status.ToString(), currentCase.Status.ToString(), authorId, authorName, $"Added {noteType} note");

        if (_hubContext != null)
        {
            await _hubContext.Clients.Group($"tenant-{tenantId}")
                .CaseNoteAdded(caseId, note.Id, authorName, content);
        }

        return note;
    }

    public Task<IReadOnlyList<CaseNote>> GetNotesAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        if (_notes.TryGetValue(caseId, out var notes))
        {
            lock (notes)
            {
                return Task.FromResult<IReadOnlyList<CaseNote>>(notes.ToList());
            }
        }

        return Task.FromResult<IReadOnlyList<CaseNote>>([]);
    }

    public Task<IReadOnlyList<CaseAuditEntry>> GetAuditTrailAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        if (_auditTrails.TryGetValue(caseId, out var trail))
        {
            lock (trail)
            {
                return Task.FromResult<IReadOnlyList<CaseAuditEntry>>(trail.ToList());
            }
        }

        return Task.FromResult<IReadOnlyList<CaseAuditEntry>>([]);
    }

    private void RecordAudit(Guid caseId, string action, string prevStatus, string newStatus, Guid actorId, string actorName, string? metadata)
    {
        var entry = new CaseAuditEntry
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Action = action,
            PreviousStatus = prevStatus,
            NewStatus = newStatus,
            ActorId = actorId,
            ActorName = actorName,
            Metadata = metadata
        };

        var list = _auditTrails.GetOrAdd(caseId, _ => []);
        lock (list)
        {
            list.Add(entry);
        }
    }

    private Task<CaseDetail> GetCaseOrThrowAsync(Guid tenantId, Guid caseId)
    {
        if (_cases.TryGetValue(caseId, out var detail) && detail.TenantId == tenantId)
        {
            return Task.FromResult(detail);
        }

        throw new KeyNotFoundException($"Case '{caseId}' was not found for tenant '{tenantId}'.");
    }
}

public sealed class PostgreSqlCaseService : ICaseService
{
    private readonly string _connectionString;
    private readonly ICaseLockManager _lockManager;
    private readonly IHubContext<CaseHub, ICaseClient>? _hubContext;

    public PostgreSqlCaseService(
        string connectionString,
        ICaseLockManager lockManager,
        IHubContext<CaseHub, ICaseClient>? hubContext = null)
    {
        _connectionString = connectionString;
        _lockManager = lockManager;
        _hubContext = hubContext;
    }

    public async Task<CaseDetail> CreateCaseAsync(CaseDetail caseDetail, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var existingById = await GetCaseByIdInternalAsync(conn, caseDetail.TenantId, caseDetail.Id, ct);
        if (existingById != null)
        {
            return existingById;
        }

        var caseNumber = caseDetail.CaseNumber;
        await using (var checkNum = new Npgsql.NpgsqlCommand("SELECT 1 FROM cases.aml_cases WHERE case_number = @num;", conn))
        {
            checkNum.Parameters.AddWithValue("num", caseNumber);
            if (await checkNum.ExecuteScalarAsync(ct) != null)
            {
                caseNumber = $"{caseNumber}-{caseDetail.Id.ToString("N")[..4].ToUpperInvariant()}";
            }
        }

        const string sql = """
            INSERT INTO cases.aml_cases (
                id, tenant_id, case_number, transaction_id, customer_id, alert_type, priority, stage, status,
                assigned_analyst_id, locked_by_user_id, locked_by_name, locked_at, lock_expires_at,
                transaction_ref_no, amount, currency, sender_name, beneficiary_name, risk_score,
                triggered_list, hold_reason, sla_due_date, resolution, resolution_reason, resolved_at, resolved_by,
                created_at, updated_at
            )
            VALUES (
                @id, @tenant_id, @case_number, @tx_id, @cust_id, @alert_type, @priority, 'INVESTIGATION', @status,
                @assigned_to, @locked_by, @locked_by_name, @locked_at, @lock_expires_at,
                @tx_ref, @amount, @currency, @sender_name, @bene_name, @risk_score,
                @triggered_list, @hold_reason, @sla_due, @resolution, @resolution_reason, @resolved_at, @resolved_by,
                @created_at, @updated_at
            )
            ON CONFLICT (id) DO NOTHING;
            """;

        await using (var cmd = new Npgsql.NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("id", caseDetail.Id);
            cmd.Parameters.AddWithValue("tenant_id", caseDetail.TenantId);
            cmd.Parameters.AddWithValue("case_number", caseNumber);
            cmd.Parameters.AddWithValue("tx_id", caseDetail.TransactionId);
            cmd.Parameters.AddWithValue("cust_id", (object?)caseDetail.CustomerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("alert_type", caseDetail.TriggeredList ?? "SANCTIONS_HIT");
            cmd.Parameters.AddWithValue("priority", caseDetail.Priority.ToString());
            cmd.Parameters.AddWithValue("status", caseDetail.Status.ToString());
            cmd.Parameters.AddWithValue("assigned_to", (object?)caseDetail.AssignedTo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("locked_by", (object?)caseDetail.LockedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("locked_by_name", (object?)caseDetail.LockedByName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("locked_at", (object?)caseDetail.LockedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("lock_expires_at", (object?)caseDetail.LockExpiresAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tx_ref", caseDetail.TransactionRefNo);
            cmd.Parameters.AddWithValue("amount", caseDetail.Amount);
            cmd.Parameters.AddWithValue("currency", caseDetail.Currency);
            cmd.Parameters.AddWithValue("sender_name", caseDetail.SenderName);
            cmd.Parameters.AddWithValue("bene_name", caseDetail.BeneficiaryName);
            cmd.Parameters.AddWithValue("risk_score", caseDetail.RiskScore);
            cmd.Parameters.AddWithValue("triggered_list", (object?)caseDetail.TriggeredList ?? DBNull.Value);
            cmd.Parameters.AddWithValue("hold_reason", caseDetail.HoldReason);
            cmd.Parameters.AddWithValue("sla_due", caseDetail.SlaDueDate);
            cmd.Parameters.AddWithValue("resolution", caseDetail.Resolution.HasValue ? (object)caseDetail.Resolution.Value.ToString() : DBNull.Value);
            cmd.Parameters.AddWithValue("resolution_reason", (object?)caseDetail.ResolutionReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("resolved_at", (object?)caseDetail.ResolvedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("resolved_by", (object?)caseDetail.ResolvedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("created_at", caseDetail.CreatedAt);
            cmd.Parameters.AddWithValue("updated_at", caseDetail.UpdatedAt);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await RecordAuditAsync(conn, caseDetail.Id, "CREATED", "NONE", caseDetail.Status.ToString(), caseDetail.TenantId, "SYSTEM", "Case initialized from screening event", ct);
        return caseDetail with { CaseNumber = caseNumber };
    }

    public async Task<CaseDetail?> GetCaseByIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return await GetCaseByIdInternalAsync(conn, tenantId, caseId, ct);
    }

    public async Task<IReadOnlyList<CaseDetail>> ListCasesAsync(Guid tenantId, CaseStatus? statusFilter = null, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT id, tenant_id, case_number, transaction_id, transaction_ref_no, customer_id,
                   amount, currency, sender_name, beneficiary_name, risk_score, priority, status,
                   triggered_list, hold_reason, sla_due_date, assigned_analyst_id,
                   locked_by_user_id, locked_by_name, locked_at, lock_expires_at,
                   resolution, resolution_reason, resolved_at, resolved_by, created_at, updated_at
            FROM cases.aml_cases
            WHERE tenant_id = @tenant_id AND (@status IS NULL OR UPPER(status) = UPPER(@status))
            ORDER BY created_at DESC;
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("status", NpgsqlTypes.NpgsqlDbType.Varchar)
        {
            Value = statusFilter.HasValue ? (object)statusFilter.Value.ToString() : DBNull.Value
        });

        var list = new List<CaseDetail>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadCaseDetail(reader));
        }

        return list;
    }

    public async Task<CaseLockResult> AcquireLockAsync(
        Guid tenantId,
        Guid caseId,
        Guid userId,
        string userName,
        TimeSpan? duration = null,
        CancellationToken ct = default)
    {
        var currentCase = await GetCaseOrThrowAsync(tenantId, caseId, ct);
        var result = await _lockManager.AcquireLockAsync(tenantId, caseId, userId, userName, duration, ct);

        if (result.IsLocked)
        {
            await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
            await conn.OpenAsync(ct);

            const string sql = """
                UPDATE cases.aml_cases
                SET locked_by_user_id = @user_id,
                    locked_by_name = @user_name,
                    locked_at = @locked_at,
                    lock_expires_at = @expires_at,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @id AND tenant_id = @tenant_id;
                """;

            await using (var cmd = new Npgsql.NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", caseId);
                cmd.Parameters.AddWithValue("tenant_id", tenantId);
                cmd.Parameters.AddWithValue("user_id", userId);
                cmd.Parameters.AddWithValue("user_name", userName);
                cmd.Parameters.AddWithValue("locked_at", (object?)result.LockedAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("expires_at", (object?)result.ExpiresAt ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await RecordAuditAsync(conn, caseId, "LOCKED", currentCase.Status.ToString(), currentCase.Status.ToString(), userId, userName, "Lock acquired for investigation", ct);

            if (_hubContext != null && result.LockedAt.HasValue)
            {
                await _hubContext.Clients.Group($"tenant-{tenantId}")
                    .CaseLocked(caseId, userId, userName, result.LockedAt.Value);
            }
        }

        return result;
    }

    public async Task<bool> ReleaseLockAsync(
        Guid tenantId,
        Guid caseId,
        Guid userId,
        bool force = false,
        CancellationToken ct = default)
    {
        var currentCase = await GetCaseOrThrowAsync(tenantId, caseId, ct);
        var released = await _lockManager.ReleaseLockAsync(tenantId, caseId, userId, force, ct);

        if (released)
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
                WHERE id = @id AND tenant_id = @tenant_id;
                """;

            await using (var cmd = new Npgsql.NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("id", caseId);
                cmd.Parameters.AddWithValue("tenant_id", tenantId);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await RecordAuditAsync(conn, caseId, "UNLOCKED", currentCase.Status.ToString(), currentCase.Status.ToString(), userId, "SYSTEM", force ? "Lock force-released" : "Lock released", ct);

            if (_hubContext != null)
            {
                await _hubContext.Clients.Group($"tenant-{tenantId}").CaseUnlocked(caseId);
            }
        }

        return released;
    }

    public async Task<CaseDetail> TransitionStatusAsync(
        Guid tenantId,
        Guid caseId,
        CaseStatus newStatus,
        Guid userId,
        string userName,
        string userRole,
        CaseResolution? resolution = null,
        string? reason = null,
        CancellationToken ct = default)
    {
        var currentCase = await GetCaseOrThrowAsync(tenantId, caseId, ct);

        _lockManager.EnsureLockHeldByUser(currentCase, userId);
        CaseStateMachine.ValidateTransition(currentCase.Status, newStatus, userRole, resolution);

        var prevStatus = currentCase.Status;
        var now = DateTimeOffset.UtcNow;
        currentCase.Status = newStatus;
        currentCase.UpdatedAt = now;

        if (resolution.HasValue)
        {
            currentCase.Resolution = resolution.Value;
            currentCase.ResolutionReason = reason;
            currentCase.ResolvedAt = now;
            currentCase.ResolvedBy = userId;
        }

        if (newStatus is CaseStatus.PendingApproval or CaseStatus.Resolved or CaseStatus.Closed)
        {
            await _lockManager.ReleaseLockAsync(tenantId, caseId, userId, force: true, ct);
            currentCase.LockedBy = null;
            currentCase.LockedByName = null;
            currentCase.LockedAt = null;
            currentCase.LockExpiresAt = null;
        }

        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            UPDATE cases.aml_cases
            SET status = @status,
                resolution = @resolution,
                resolution_reason = @resolution_reason,
                resolved_at = @resolved_at,
                resolved_by = @resolved_by,
                locked_by_user_id = @locked_by,
                locked_by_name = @locked_by_name,
                locked_at = @locked_at,
                lock_expires_at = @lock_expires_at,
                updated_at = @updated_at
            WHERE id = @id AND tenant_id = @tenant_id;
            """;

        await using (var cmd = new Npgsql.NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("id", caseId);
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("status", currentCase.Status.ToString());
            cmd.Parameters.AddWithValue("resolution", currentCase.Resolution.HasValue ? (object)currentCase.Resolution.Value.ToString() : DBNull.Value);
            cmd.Parameters.AddWithValue("resolution_reason", (object?)currentCase.ResolutionReason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("resolved_at", (object?)currentCase.ResolvedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("resolved_by", (object?)currentCase.ResolvedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("locked_by", (object?)currentCase.LockedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("locked_by_name", (object?)currentCase.LockedByName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("locked_at", (object?)currentCase.LockedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("lock_expires_at", (object?)currentCase.LockExpiresAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("updated_at", currentCase.UpdatedAt);

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await RecordAuditAsync(conn, caseId, "STATUS_CHANGE", prevStatus.ToString(), newStatus.ToString(), userId, userName, reason, ct);

        if (_hubContext != null)
        {
            await _hubContext.Clients.Group($"tenant-{tenantId}")
                .CaseStatusChanged(caseId, newStatus.ToString(), resolution?.ToString(), userName);
        }

        return currentCase;
    }

    public async Task<CaseNote> AddNoteAsync(
        Guid tenantId,
        Guid caseId,
        Guid authorId,
        string authorName,
        string content,
        string noteType = "INVESTIGATION",
        CancellationToken ct = default)
    {
        var currentCase = await GetCaseOrThrowAsync(tenantId, caseId, ct);

        var note = new CaseNote
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            AuthorId = authorId,
            AuthorName = authorName,
            Content = content,
            NoteType = noteType
        };

        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            INSERT INTO cases.case_notes (id, case_id, author_id, author_name, note_type, content, created_at)
            VALUES (@id, @case_id, @author_id, @author_name, @note_type, @content, @created_at);
            """;

        await using (var cmd = new Npgsql.NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("id", note.Id);
            cmd.Parameters.AddWithValue("case_id", note.CaseId);
            cmd.Parameters.AddWithValue("author_id", note.AuthorId);
            cmd.Parameters.AddWithValue("author_name", note.AuthorName);
            cmd.Parameters.AddWithValue("note_type", note.NoteType);
            cmd.Parameters.AddWithValue("content", note.Content);
            cmd.Parameters.AddWithValue("created_at", note.CreatedAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await RecordAuditAsync(conn, caseId, "NOTE_ADDED", currentCase.Status.ToString(), currentCase.Status.ToString(), authorId, authorName, $"Added {noteType} note", ct);

        if (_hubContext != null)
        {
            await _hubContext.Clients.Group($"tenant-{tenantId}")
                .CaseNoteAdded(caseId, note.Id, authorName, content);
        }

        return note;
    }

    public async Task<IReadOnlyList<CaseNote>> GetNotesAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT n.id, n.case_id, n.author_id, n.author_name, n.note_type, n.content, n.created_at
            FROM cases.case_notes n
            INNER JOIN cases.aml_cases c ON c.id = n.case_id
            WHERE n.case_id = @case_id AND c.tenant_id = @tenant_id
            ORDER BY n.created_at ASC;
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("case_id", caseId);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        var list = new List<CaseNote>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new CaseNote
            {
                Id = reader.GetGuid(0),
                CaseId = reader.GetGuid(1),
                AuthorId = reader.GetGuid(2),
                AuthorName = reader.GetString(3),
                NoteType = reader.GetString(4),
                Content = reader.GetString(5),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(6)
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<CaseAuditEntry>> GetAuditTrailAsync(Guid tenantId, Guid caseId, CancellationToken ct = default)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            SELECT a.id, a.case_id, a.action, a.previous_status, a.new_status, a.actor_id, a.actor_name, a.metadata, a.timestamp
            FROM cases.case_audit_entries a
            INNER JOIN cases.aml_cases c ON c.id = a.case_id
            WHERE a.case_id = @case_id AND c.tenant_id = @tenant_id
            ORDER BY a.timestamp ASC;
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("case_id", caseId);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        var list = new List<CaseAuditEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new CaseAuditEntry
            {
                Id = reader.GetGuid(0),
                CaseId = reader.GetGuid(1),
                Action = reader.GetString(2),
                PreviousStatus = reader.GetString(3),
                NewStatus = reader.GetString(4),
                ActorId = reader.GetGuid(5),
                ActorName = reader.GetString(6),
                Metadata = reader.IsDBNull(7) ? null : reader.GetString(7),
                Timestamp = reader.GetFieldValue<DateTimeOffset>(8)
            });
        }

        return list;
    }

    private static async Task RecordAuditAsync(
        Npgsql.NpgsqlConnection conn,
        Guid caseId,
        string action,
        string prevStatus,
        string newStatus,
        Guid actorId,
        string actorName,
        string? metadata,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO cases.case_audit_entries (id, case_id, action, previous_status, new_status, actor_id, actor_name, metadata, timestamp)
            VALUES (@id, @case_id, @action, @prev, @next, @actor_id, @actor_name, @meta, CURRENT_TIMESTAMP);
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("case_id", caseId);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("prev", prevStatus);
        cmd.Parameters.AddWithValue("next", newStatus);
        cmd.Parameters.AddWithValue("actor_id", actorId);
        cmd.Parameters.AddWithValue("actor_name", actorName);
        cmd.Parameters.AddWithValue("meta", (object?)metadata ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<CaseDetail> GetCaseOrThrowAsync(Guid tenantId, Guid caseId, CancellationToken ct)
    {
        var detail = await GetCaseByIdAsync(tenantId, caseId, ct);
        if (detail != null)
        {
            return detail;
        }

        throw new KeyNotFoundException($"Case '{caseId}' was not found for tenant '{tenantId}'.");
    }

    private static async Task<CaseDetail?> GetCaseByIdInternalAsync(Npgsql.NpgsqlConnection conn, Guid tenantId, Guid caseId, CancellationToken ct)
    {
        const string sql = """
            SELECT id, tenant_id, case_number, transaction_id, transaction_ref_no, customer_id,
                   amount, currency, sender_name, beneficiary_name, risk_score, priority, status,
                   triggered_list, hold_reason, sla_due_date, assigned_analyst_id,
                   locked_by_user_id, locked_by_name, locked_at, lock_expires_at,
                   resolution, resolution_reason, resolved_at, resolved_by, created_at, updated_at
            FROM cases.aml_cases
            WHERE id = @id AND tenant_id = @tenant_id;
            """;

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", caseId);
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadCaseDetail(reader);
        }

        return null;
    }

    private static CaseDetail ReadCaseDetail(Npgsql.NpgsqlDataReader r)
    {
        var priorityStr = r.GetString(11);
        var statusStr = r.GetString(12);
        var resStr = r.IsDBNull(21) ? null : r.GetString(21);

        return new CaseDetail
        {
            Id = r.GetGuid(0),
            TenantId = r.GetGuid(1),
            CaseNumber = r.GetString(2),
            TransactionId = r.IsDBNull(3) ? Guid.Empty : r.GetGuid(3),
            TransactionRefNo = r.GetString(4),
            CustomerId = r.IsDBNull(5) ? null : r.GetGuid(5),
            Amount = r.GetDecimal(6),
            Currency = r.GetString(7),
            SenderName = r.GetString(8),
            BeneficiaryName = r.GetString(9),
            RiskScore = r.GetDecimal(10),
            Priority = Enum.TryParse<CasePriority>(priorityStr, true, out var p) ? p : CasePriority.High,
            Status = Enum.TryParse<CaseStatus>(statusStr, true, out var s) ? s : CaseStatus.Open,
            TriggeredList = r.IsDBNull(13) ? null : r.GetString(13),
            HoldReason = r.GetString(14),
            SlaDueDate = r.GetFieldValue<DateTimeOffset>(15),
            AssignedTo = r.IsDBNull(16) ? null : r.GetGuid(16),
            LockedBy = r.IsDBNull(17) ? null : r.GetGuid(17),
            LockedByName = r.IsDBNull(18) ? null : r.GetString(18),
            LockedAt = r.IsDBNull(19) ? null : r.GetFieldValue<DateTimeOffset>(19),
            LockExpiresAt = r.IsDBNull(20) ? null : r.GetFieldValue<DateTimeOffset>(20),
            Resolution = resStr != null && Enum.TryParse<CaseResolution>(resStr, true, out var res) ? res : null,
            ResolutionReason = r.IsDBNull(22) ? null : r.GetString(22),
            ResolvedAt = r.IsDBNull(23) ? null : r.GetFieldValue<DateTimeOffset>(23),
            ResolvedBy = r.IsDBNull(24) ? null : r.GetGuid(24),
            CreatedAt = r.GetFieldValue<DateTimeOffset>(25),
            UpdatedAt = r.GetFieldValue<DateTimeOffset>(26)
        };
    }
}

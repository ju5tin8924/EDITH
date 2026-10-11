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

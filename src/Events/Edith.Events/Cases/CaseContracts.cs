using System.Collections.Concurrent;
using Edith.Events.Contracts;

namespace Edith.Events.Cases;

public sealed record CaseRecord
{
    public required Guid Id { get; init; } = Guid.NewGuid();
    public required Guid TenantId { get; init; }
    public required string CaseNumber { get; init; }
    public required Guid TransactionId { get; init; }
    public required string TransactionRefNo { get; init; }
    public Guid? CustomerId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string SenderName { get; init; }
    public required string BeneficiaryName { get; init; }
    public required decimal RiskScore { get; init; }
    public required string Priority { get; init; } // CRITICAL | HIGH | MEDIUM | LOW
    public required string Status { get; set; } = "OPEN";
    public string? TriggeredList { get; init; }
    public required string HoldReason { get; init; }
    public required DateTimeOffset SlaDueDate { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? AssignedTo { get; set; }
}

public interface ICaseRepository
{
    Task<CaseRecord> CreateCaseAsync(CaseRecord caseRecord, CancellationToken ct = default);
    Task<CaseRecord?> FindByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default);
    Task<CaseRecord?> FindByTransactionIdAsync(Guid tenantId, Guid transactionId, CancellationToken ct = default);
    Task<IReadOnlyList<CaseRecord>> ListOpenCasesAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed class InMemoryCaseRepository : ICaseRepository
{
    private readonly ConcurrentDictionary<Guid, CaseRecord> _cases = new();

    public Task<CaseRecord> CreateCaseAsync(CaseRecord caseRecord, CancellationToken ct = default)
    {
        _cases[caseRecord.Id] = caseRecord;
        return Task.FromResult(caseRecord);
    }

    public Task<CaseRecord?> FindByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default)
    {
        var found = _cases.Values.FirstOrDefault(c => c.TenantId == tenantId && string.Equals(c.CaseNumber, caseNumber, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(found);
    }

    public Task<CaseRecord?> FindByTransactionIdAsync(Guid tenantId, Guid transactionId, CancellationToken ct = default)
    {
        var found = _cases.Values.FirstOrDefault(c => c.TenantId == tenantId && c.TransactionId == transactionId);
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<CaseRecord>> ListOpenCasesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var list = _cases.Values.Where(c => c.TenantId == tenantId && c.Status == "OPEN").ToList();
        return Task.FromResult<IReadOnlyList<CaseRecord>>(list);
    }
}

public interface ICaseAutoCreator
{
    Task<CaseRecord> HandleTransactionHeldAsync(TransactionHeldEvent evt, CancellationToken ct = default);
}

public sealed class CaseAutoCreator : ICaseAutoCreator
{
    private readonly ICaseRepository _caseRepo;

    public CaseAutoCreator(ICaseRepository caseRepo)
    {
        _caseRepo = caseRepo;
    }

    public async Task<CaseRecord> HandleTransactionHeldAsync(TransactionHeldEvent evt, CancellationToken ct = default)
    {
        // 1. Determine priority from score
        var priority = evt.OverallScore >= 90.0m
            ? "CRITICAL"
            : (evt.OverallScore >= 80.0m ? "HIGH" : "MEDIUM");

        // 2. PRD Mandate: 3-day SLA for EDD
        var slaDueDate = evt.Timestamp.AddDays(3);

        var newCase = new CaseRecord
        {
            Id = Guid.NewGuid(),
            TenantId = evt.TenantId,
            CaseNumber = evt.CaseNumber,
            TransactionId = evt.TransactionId,
            TransactionRefNo = evt.TransactionRefNo,
            CustomerId = evt.CustomerId,
            Amount = evt.Amount,
            Currency = evt.Currency,
            SenderName = evt.SenderName,
            BeneficiaryName = evt.BeneficiaryName,
            RiskScore = evt.OverallScore,
            Priority = priority,
            Status = "OPEN",
            TriggeredList = evt.TriggeredList,
            HoldReason = evt.HoldReason,
            SlaDueDate = slaDueDate,
            CreatedAt = evt.Timestamp
        };

        return await _caseRepo.CreateCaseAsync(newCase, ct);
    }
}

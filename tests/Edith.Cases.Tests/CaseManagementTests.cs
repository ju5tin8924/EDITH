using Edith.Cases.Locking;
using Edith.Cases.Models;
using Edith.Cases.Services;

namespace Edith.Cases.Tests;

public sealed class CaseManagementTests
{
    private static readonly Guid TestTenantId = Guid.Parse("01931000-0000-7000-8000-000000000001");
    private static readonly Guid AnalystAId = Guid.Parse("01931000-0000-7000-8000-000000000010");
    private static readonly Guid AnalystBId = Guid.Parse("01931000-0000-7000-8000-000000000020");
    private static readonly Guid MlroId = Guid.Parse("01931000-0000-7000-8000-000000000030");

    private static CaseDetail CreateSampleCase()
    {
        return new CaseDetail
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantId,
            CaseNumber = $"CASE-20261011-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            TransactionId = Guid.NewGuid(),
            TransactionRefNo = "TX-CASE-001",
            Amount = 500000m,
            Currency = "AED",
            SenderName = "Acme Global Trading",
            BeneficiaryName = "Viktor Anatolyevich Bout",
            RiskScore = 98.5m,
            Priority = CasePriority.Critical,
            Status = CaseStatus.Open,
            HoldReason = "OFAC_SDN Sanctions Match",
            SlaDueDate = DateTimeOffset.UtcNow.AddDays(3)
        };
    }

    [Fact]
    public async Task Legal_Workflow_Progression_Succeeds_Through_All_Lifecycle_Stages()
    {
        var lockManager = new CaseLockManager();
        var caseService = new CaseService(lockManager);

        var sampleCase = CreateSampleCase();
        await caseService.CreateCaseAsync(sampleCase);

        // 1. Analyst A locks case
        var lockResult = await caseService.AcquireLockAsync(TestTenantId, sampleCase.Id, AnalystAId, "Analyst Fatima");
        Assert.True(lockResult.IsLocked);

        // 2. Open -> UnderReview
        var underReview = await caseService.TransitionStatusAsync(
            TestTenantId, sampleCase.Id, CaseStatus.UnderReview,
            AnalystAId, "Analyst Fatima", "ANALYST");
        Assert.Equal(CaseStatus.UnderReview, underReview.Status);

        // 3. Add note
        await caseService.AddNoteAsync(
            TestTenantId, sampleCase.Id, AnalystAId, "Analyst Fatima",
            "Matched target name against OFAC SDN 9812. Escalating to MLRO for freeze confirmation.");

        // 4. UnderReview -> PendingApproval
        var pendingApproval = await caseService.TransitionStatusAsync(
            TestTenantId, sampleCase.Id, CaseStatus.PendingApproval,
            AnalystAId, "Analyst Fatima", "ANALYST",
            reason: "Escalated for STR filing and asset freeze.");
        Assert.Equal(CaseStatus.PendingApproval, pendingApproval.Status);

        // 5. MLRO resolves: PendingApproval -> Resolved (ReportedStrFreeze)
        var resolved = await caseService.TransitionStatusAsync(
            TestTenantId, sampleCase.Id, CaseStatus.Resolved,
            MlroId, "MLRO Tariq", "MLRO",
            resolution: CaseResolution.ReportedStrFreeze,
            reason: "STR filed with UAE FIU via goAML. Asset freeze mandated.");
        Assert.Equal(CaseStatus.Resolved, resolved.Status);
        Assert.Equal(CaseResolution.ReportedStrFreeze, resolved.Resolution);
        Assert.Equal(MlroId, resolved.ResolvedBy);

        // 6. Resolved -> Closed
        var closed = await caseService.TransitionStatusAsync(
            TestTenantId, sampleCase.Id, CaseStatus.Closed,
            MlroId, "MLRO Tariq", "MLRO",
            reason: "Completed regulatory notification and CBS webhook callback.");
        Assert.Equal(CaseStatus.Closed, closed.Status);

        // 7. Verify audit trail
        var audit = await caseService.GetAuditTrailAsync(TestTenantId, sampleCase.Id);
        Assert.True(audit.Count >= 5);
        Assert.Contains(audit, a => a.Action == "CREATED");
        Assert.Contains(audit, a => a.Action == "LOCKED");
        Assert.Contains(audit, a => a.Action == "STATUS_CHANGE" && a.NewStatus == "UnderReview");
        Assert.Contains(audit, a => a.Action == "STATUS_CHANGE" && a.NewStatus == "PendingApproval");
        Assert.Contains(audit, a => a.Action == "STATUS_CHANGE" && a.NewStatus == "Resolved");
        Assert.Contains(audit, a => a.Action == "STATUS_CHANGE" && a.NewStatus == "Closed");
    }

    [Theory]
    [InlineData(CaseStatus.Open, CaseStatus.Closed)]
    [InlineData(CaseStatus.Open, CaseStatus.Resolved)]
    [InlineData(CaseStatus.Closed, CaseStatus.Open)]
    [InlineData(CaseStatus.Closed, CaseStatus.UnderReview)]
    public async Task Illegal_Transitions_Are_Rejected_With_InvalidCaseTransitionException(CaseStatus from, CaseStatus to)
    {
        var lockManager = new CaseLockManager();
        var caseService = new CaseService(lockManager);

        var sampleCase = CreateSampleCase();
        sampleCase.Status = from;
        await caseService.CreateCaseAsync(sampleCase);

        await Assert.ThrowsAsync<InvalidCaseTransitionException>(() =>
            caseService.TransitionStatusAsync(
                TestTenantId, sampleCase.Id, to,
                MlroId, "MLRO Tariq", "MLRO",
                resolution: CaseResolution.ReportedStrFreeze));
    }

    [Fact]
    public async Task Two_Analyst_Lock_Contention_Blocks_Second_Analyst()
    {
        var lockManager = new CaseLockManager();
        var caseService = new CaseService(lockManager);

        var sampleCase = CreateSampleCase();
        await caseService.CreateCaseAsync(sampleCase);

        // 1. Analyst A locks case
        var lockA = await caseService.AcquireLockAsync(TestTenantId, sampleCase.Id, AnalystAId, "Analyst Fatima");
        Assert.True(lockA.IsLocked);
        Assert.Equal(AnalystAId, lockA.LockedBy);

        // 2. Analyst B attempts to lock same case
        var lockB = await caseService.AcquireLockAsync(TestTenantId, sampleCase.Id, AnalystBId, "Analyst Khalid");
        Assert.False(lockB.IsLocked);
        Assert.Equal(AnalystAId, lockB.LockedBy);
        Assert.Equal("Analyst Fatima", lockB.LockedByName);
        Assert.Contains("Analyst Fatima", lockB.ErrorMessage);
    }

    [Fact]
    public async Task Non_Lock_Holder_Cannot_Transition_Case()
    {
        var lockManager = new CaseLockManager();
        var caseService = new CaseService(lockManager);

        var sampleCase = CreateSampleCase();
        await caseService.CreateCaseAsync(sampleCase);

        // Analyst A holds lock
        await caseService.AcquireLockAsync(TestTenantId, sampleCase.Id, AnalystAId, "Analyst Fatima");

        // Analyst B tries to change status
        await Assert.ThrowsAsync<CaseLockedByAnotherUserException>(() =>
            caseService.TransitionStatusAsync(
                TestTenantId, sampleCase.Id, CaseStatus.UnderReview,
                AnalystBId, "Analyst Khalid", "ANALYST"));
    }

    [Fact]
    public async Task Lock_Release_Allows_Subsequent_Analyst_To_Acquire()
    {
        var lockManager = new CaseLockManager();
        var caseService = new CaseService(lockManager);

        var sampleCase = CreateSampleCase();
        await caseService.CreateCaseAsync(sampleCase);

        // Analyst A acquires then releases lock
        await caseService.AcquireLockAsync(TestTenantId, sampleCase.Id, AnalystAId, "Analyst Fatima");
        var released = await caseService.ReleaseLockAsync(TestTenantId, sampleCase.Id, AnalystAId);
        Assert.True(released);

        // Analyst B can now acquire lock
        var lockB = await caseService.AcquireLockAsync(TestTenantId, sampleCase.Id, AnalystBId, "Analyst Khalid");
        Assert.True(lockB.IsLocked);
        Assert.Equal(AnalystBId, lockB.LockedBy);
    }

    [Fact]
    public async Task Analyst_Cannot_Resolve_Case_Without_MLRO_Role()
    {
        var lockManager = new CaseLockManager();
        var caseService = new CaseService(lockManager);

        var sampleCase = CreateSampleCase();
        sampleCase.Status = CaseStatus.PendingApproval;
        await caseService.CreateCaseAsync(sampleCase);

        // Analyst attempting to resolve
        await Assert.ThrowsAsync<UnauthorizedCaseActionException>(() =>
            caseService.TransitionStatusAsync(
                TestTenantId, sampleCase.Id, CaseStatus.Resolved,
                AnalystAId, "Analyst Fatima", "ANALYST",
                resolution: CaseResolution.ClearedFalsePositive));

        // MLRO attempting to resolve -> Succeeds
        var resolved = await caseService.TransitionStatusAsync(
            TestTenantId, sampleCase.Id, CaseStatus.Resolved,
            MlroId, "MLRO Tariq", "MLRO",
            resolution: CaseResolution.ClearedFalsePositive,
            reason: "False positive verified via passport verification.");

        Assert.Equal(CaseStatus.Resolved, resolved.Status);
    }

    [Fact]
    public async Task Case_Resolution_Requires_Disposition_Type()
    {
        var lockManager = new CaseLockManager();
        var caseService = new CaseService(lockManager);

        var sampleCase = CreateSampleCase();
        sampleCase.Status = CaseStatus.PendingApproval;
        await caseService.CreateCaseAsync(sampleCase);

        // MLRO attempts to resolve without resolution argument
        await Assert.ThrowsAsync<InvalidCaseTransitionException>(() =>
            caseService.TransitionStatusAsync(
                TestTenantId, sampleCase.Id, CaseStatus.Resolved,
                MlroId, "MLRO Tariq", "MLRO",
                resolution: null));
    }
}

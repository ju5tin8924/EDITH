namespace Edith.Cases.Models;

public enum CaseStatus
{
    Open,
    UnderReview,
    PendingApproval,
    Resolved,
    Closed
}

public enum CasePriority
{
    Critical,
    High,
    Medium,
    Low
}

public enum CaseResolution
{
    ClearedFalsePositive,
    ReportedStrFreeze,
    ReportedStrRelease,
    Dismissed
}

public class InvalidCaseTransitionException : Exception
{
    public InvalidCaseTransitionException(CaseStatus from, CaseStatus to, string reason)
        : base($"Cannot transition case from {from} to {to}: {reason}") { }
}

public class CaseAlreadyLockedException : Exception
{
    public Guid LockedBy { get; }
    public string LockedByName { get; }

    public CaseAlreadyLockedException(Guid lockedBy, string lockedByName)
        : base($"This case is currently locked by {lockedByName} ({lockedBy}).")
    {
        LockedBy = lockedBy;
        LockedByName = lockedByName;
    }
}

public class CaseLockedByAnotherUserException : Exception
{
    public CaseLockedByAnotherUserException(Guid lockedBy, string lockedByName)
        : base($"Action blocked: Case is currently being investigated by {lockedByName}.") { }
}

public class UnauthorizedCaseActionException : Exception
{
    public UnauthorizedCaseActionException(string message) : base(message) { }
}

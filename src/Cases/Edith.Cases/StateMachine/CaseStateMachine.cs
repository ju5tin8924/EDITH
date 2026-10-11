using Edith.Cases.Models;

namespace Edith.Cases.StateMachine;

public static class CaseStateMachine
{
    private static readonly HashSet<string> ResolutionRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "MLRO",
        "COMPLIANCE_OFFICER",
        "SUPER_ADMIN"
    };

    public static bool IsValidTransition(CaseStatus current, CaseStatus target)
    {
        return (current, target) switch
        {
            (CaseStatus.Open, CaseStatus.UnderReview) => true,
            (CaseStatus.UnderReview, CaseStatus.PendingApproval) => true,
            (CaseStatus.UnderReview, CaseStatus.Resolved) => true,
            (CaseStatus.PendingApproval, CaseStatus.Resolved) => true,
            (CaseStatus.PendingApproval, CaseStatus.UnderReview) => true, // Sent back for rework
            (CaseStatus.Resolved, CaseStatus.Closed) => true,
            _ => false
        };
    }

    public static void ValidateTransition(
        CaseStatus current,
        CaseStatus target,
        string role,
        CaseResolution? resolution = null)
    {
        if (current == target)
        {
            return;
        }

        if (!IsValidTransition(current, target))
        {
            throw new InvalidCaseTransitionException(current, target, $"Transition from {current} to {target} is not permitted by the compliance lifecycle workflow.");
        }

        // Resolving a case requires an elevated compliance role (MLRO / Compliance Officer)
        if (target == CaseStatus.Resolved)
        {
            if (!ResolutionRoles.Contains(role))
            {
                throw new UnauthorizedCaseActionException($"Role '{role}' is not authorized to resolve cases. Only MLRO or Compliance Officers may execute case resolutions.");
            }

            if (resolution == null)
            {
                throw new InvalidCaseTransitionException(current, target, "A valid resolution disposition (e.g. ClearedFalsePositive, ReportedStrFreeze, ReportedStrRelease) must be specified when resolving a case.");
            }
        }
    }
}

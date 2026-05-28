using MotorInsurance.Domain.Common;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Domain.StateMachines;

public static class PolicyStateMachine
{
    private static readonly Dictionary<PolicyStatus, PolicyStatus[]> Allowed = new()
    {
        [PolicyStatus.Draft]     = new[] { PolicyStatus.Quoted, PolicyStatus.Cancelled },
        [PolicyStatus.Quoted]    = new[] { PolicyStatus.Issued, PolicyStatus.Cancelled, PolicyStatus.Expired },
        [PolicyStatus.Issued]    = new[] { PolicyStatus.Active, PolicyStatus.Cancelled },
        [PolicyStatus.Active]    = new[] { PolicyStatus.Cancelled, PolicyStatus.Expired },
        [PolicyStatus.Cancelled] = Array.Empty<PolicyStatus>(),
        [PolicyStatus.Expired]   = Array.Empty<PolicyStatus>(),
    };

    public static bool CanTransition(PolicyStatus from, PolicyStatus to) =>
        Allowed.TryGetValue(from, out var next) && Array.IndexOf(next, to) >= 0;

    public static void EnsureTransition(PolicyStatus from, PolicyStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidStateTransitionException("policy", from.ToString(), to.ToString());
    }
}

public static class ClaimStateMachine
{
    private static readonly Dictionary<ClaimStatus, ClaimStatus[]> Allowed = new()
    {
        [ClaimStatus.Filed]       = new[] { ClaimStatus.UnderReview },
        [ClaimStatus.UnderReview] = new[] { ClaimStatus.Assessment },
        [ClaimStatus.Assessment]  = new[] { ClaimStatus.Approved, ClaimStatus.Rejected },
        [ClaimStatus.Approved]    = new[] { ClaimStatus.Paid },
        [ClaimStatus.Paid]        = new[] { ClaimStatus.Closed },
        [ClaimStatus.Rejected]    = new[] { ClaimStatus.Closed },
        [ClaimStatus.Closed]      = Array.Empty<ClaimStatus>(),
    };

    public static bool CanTransition(ClaimStatus from, ClaimStatus to) =>
        Allowed.TryGetValue(from, out var next) && Array.IndexOf(next, to) >= 0;

    public static void EnsureTransition(ClaimStatus from, ClaimStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidStateTransitionException("claim", from.ToString(), to.ToString());
    }
}

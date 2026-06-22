using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Claims;

/// <summary>
/// Target service-level days a claim should spend in each status before it counts as overdue.
/// Single source of truth shared by the aging worklist (<c>ClaimsAgingEndpoint</c>) and the SLA
/// auto-escalation pass (<see cref="ClaimEscalation"/>).
/// </summary>
public static class ClaimSla
{
    /// <summary>Fallback target for any status not explicitly listed.</summary>
    private const int DefaultDays = 5;

    private static readonly IReadOnlyDictionary<ClaimStatus, int> Targets = new Dictionary<ClaimStatus, int>
    {
        [ClaimStatus.Filed] = 2,
        [ClaimStatus.UnderReview] = 3,
        [ClaimStatus.Assessment] = 5,
        [ClaimStatus.Approved] = 3,
        [ClaimStatus.Paid] = 2,
        [ClaimStatus.Rejected] = 2,
    };

    /// <summary>Target days a claim should spend in <paramref name="status"/> before it is overdue.</summary>
    public static int DaysFor(ClaimStatus status) => Targets.TryGetValue(status, out var d) ? d : DefaultDays;
}

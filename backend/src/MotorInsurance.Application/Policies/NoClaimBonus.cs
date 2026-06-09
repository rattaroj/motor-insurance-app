using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Policies;

/// <summary>
/// No-claim-bonus rule shared by renewal rating and the NCB certificate: a policy is "claim-free"
/// for NCB purposes when it has no claims other than rejected ones. Keeping this in one place stops
/// the renewal step-up logic and the certificate from drifting apart.
/// </summary>
public static class NoClaimBonus
{
    /// <summary>True when the policy has no non-rejected claim (i.e. it earns an NCB step-up on renewal).</summary>
    public static async Task<bool> IsClaimFreeAsync(IAppDbContext db, long policyId, CancellationToken ct) =>
        !await db.Claims.AnyAsync(c => c.PolicyId == policyId && c.Status != ClaimStatus.Rejected, ct);
}

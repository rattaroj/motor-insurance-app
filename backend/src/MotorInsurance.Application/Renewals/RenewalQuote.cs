using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Policies;
using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Renewals;

/// <summary>The computed terms of a renewal: when it starts, the stepped NCB, carried riders, and the rated premium.</summary>
public record RenewalTerms(
    DateOnly NewEffective, int NewNcb, decimal SumInsured,
    IReadOnlyList<long> RiderIds, PremiumBreakdown Breakdown);

/// <summary>
/// Single source of truth for renewal premium rating, shared by the actual renewal
/// (<c>RenewPolicyEndpoint.RenewAsync</c>) and the renewal-reminder quote, so the quoted price
/// in a reminder always matches what the customer is later charged. Carries over the previous
/// policy's coverage/deductible/riders and steps the NCB by the previous year's claim history.
/// </summary>
public static class RenewalQuote
{
    /// <summary>
    /// Rate the renewal of an already-loaded <paramref name="prev"/> policy (must have its
    /// <see cref="Policy.Riders"/> loaded and a non-null <see cref="Policy.ExpiryDate"/>).
    /// </summary>
    public static async Task<RenewalTerms> ComputeTermsAsync(
        IAppDbContext db, Policy prev, decimal? adjustedSumInsured, CancellationToken ct)
    {
        var newEffective = prev.ExpiryDate!.Value.AddDays(1);
        var sumInsured = adjustedSumInsured ?? prev.SumInsured;

        // No-claim bonus: a claim-free previous year bumps the NCB step up; any (non-rejected) claim resets it.
        var claimFree = await NoClaimBonus.IsClaimFreeAsync(db, prev.Id, ct);
        var newNcb = claimFree
            ? PremiumCalculator.StepUpNcb(prev.NcbPercent)
            : PremiumCalculator.StepDownNcb(prev.NcbPercent);

        var riderIds = prev.Riders.Select(pr => pr.RiderId).ToList();

        var rating = await PremiumRatingService.RateAsync(
            db, newEffective.Year, prev.VehicleId, prev.CoverageType, sumInsured,
            newNcb, prev.Deductible, riderIds, ct);

        return new RenewalTerms(newEffective, newNcb, sumInsured, riderIds, rating.Breakdown);
    }

    /// <summary>
    /// Estimate a policy's renewal premium without persisting anything (for reminders/previews).
    /// Returns null when the policy can't be quoted (missing, or no expiry date).
    /// </summary>
    public static async Task<RenewalTerms?> EstimateAsync(IAppDbContext db, long policyId, CancellationToken ct)
    {
        var prev = await db.Policies.AsNoTracking()
            .Include(p => p.Riders)
            .FirstOrDefaultAsync(p => p.Id == policyId, ct);
        if (prev?.ExpiryDate is null) return null;

        return await ComputeTermsAsync(db, prev, adjustedSumInsured: null, ct);
    }
}

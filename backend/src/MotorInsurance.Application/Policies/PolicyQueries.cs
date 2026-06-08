using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Policies;

/// <summary>
/// Single source of truth for the renewal-eligibility rules that were previously duplicated
/// across the expiring-policies worklist, the lifecycle worker, and the renew command:
/// "Active and inside the pre-expiry window" + "not yet renewed".
/// Composable <see cref="IQueryable{T}"/> extensions (CLAUDE.md: shared query logic lives in
/// Application as a plain helper, not a repository).
/// </summary>
public static class PolicyQueries
{
    /// <summary>The standard pre-expiry renewal window, in days.</summary>
    public const int RenewalWindowDays = 60;

    /// <summary>
    /// Active policies whose expiry falls within <paramref name="days"/> of <paramref name="today"/>
    /// (inclusive, and not already past). <paramref name="today"/> is a <see cref="DateOnly"/> so the
    /// comparison stays date-only and translates to SQL.
    /// </summary>
    public static IQueryable<Policy> ExpiringWithin(this IQueryable<Policy> policies, DateOnly today, int days)
    {
        var limit = today.AddDays(days); // computed outside the expression so EF treats it as a constant
        return policies.Where(p => p.Status == PolicyStatus.Active
            && p.ExpiryDate != null && p.ExpiryDate >= today && p.ExpiryDate <= limit);
    }

    /// <summary>
    /// Policies that have not been renewed yet — i.e. no other policy links back to them via
    /// <see cref="Policy.PreviousPolicyId"/>.
    /// </summary>
    public static IQueryable<Policy> NotYetRenewed(this IQueryable<Policy> policies, IAppDbContext db)
        => policies.Where(p => !db.Policies.Any(r => r.PreviousPolicyId == p.Id));

    /// <summary>True once a renewal policy exists that links back to <paramref name="policyId"/>.</summary>
    public static Task<bool> HasBeenRenewedAsync(this IAppDbContext db, long policyId, CancellationToken ct)
        => db.Policies.AnyAsync(r => r.PreviousPolicyId == policyId, ct);
}

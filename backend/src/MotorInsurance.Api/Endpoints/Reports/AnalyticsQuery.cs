using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Api.Endpoints.Reports;

/// <summary>
/// Shared reporting aggregation used by both the analytics JSON endpoint and its CSV export.
/// When <paramref name="from"/>/<paramref name="to"/> are supplied, every figure is scoped to that
/// date range; otherwise it falls back to all-time totals with a rolling 12-month premium chart.
/// </summary>
public static class AnalyticsQuery
{
    public static async Task<AnalyticsDto> ComputeAsync(
        IAppDbContext db, IDateTimeProvider clock, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var hasRange = from is not null || to is not null;
        var now = clock.UtcNow;

        // Effective window: explicit range, else a rolling 12-month window ending this month.
        var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var fromDt = from?.ToDateTime(TimeOnly.MinValue) ?? thisMonth.AddMonths(-11);
        // Inclusive upper bound: end of the "to" day, else end of this month.
        var toDt = to?.ToDateTime(TimeOnly.MaxValue) ?? thisMonth.AddMonths(1).AddTicks(-1);

        // Premium written per month across the window (zero-filled, capped at 36 months).
        var grouped = await db.Policies.AsNoTracking()
            .Where(p => p.CreatedAt >= fromDt && p.CreatedAt <= toDt)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Premium = g.Sum(x => x.Premium) })
            .ToListAsync(ct);

        var premiumByMonth = new List<MonthPremium>();
        var cursor = new DateTime(fromDt.Year, fromDt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonth = new DateTime(toDt.Year, toDt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; cursor <= lastMonth && i < 36; cursor = cursor.AddMonths(1), i++)
        {
            var hit = grouped.FirstOrDefault(x => x.Year == cursor.Year && x.Month == cursor.Month);
            premiumByMonth.Add(new MonthPremium(cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture), hit?.Premium ?? 0m));
        }

        // Totals + breakdowns: range-scoped when a range was given, else all-time.
        var policies = db.Policies.AsNoTracking();
        var claims = db.Claims.AsNoTracking();
        var payments = db.Payments.AsNoTracking();
        if (hasRange)
        {
            policies = policies.Where(p => p.CreatedAt >= fromDt && p.CreatedAt <= toDt);
            claims = claims.Where(c => c.CreatedAt >= fromDt && c.CreatedAt <= toDt);
            payments = payments.Where(p => p.PaidAt != null && p.PaidAt >= fromDt && p.PaidAt <= toDt);
        }

        var premiumWritten = await policies.SumAsync(p => (decimal?)p.Premium, ct) ?? 0m;
        var claimsPaid = await payments
            .Where(p => p.Direction == PaymentDirection.Outbound && p.Status == PaymentStatus.Paid)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var polByStatus = (await policies
            .GroupBy(p => p.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new LabelCount(x.Key.ToString(), x.Count)).ToList();
        var polByCoverage = (await policies
            .GroupBy(p => p.CoverageType).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new LabelCount(x.Key.ToString(), x.Count)).ToList();
        var claimsByStatus = (await claims
            .GroupBy(c => c.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new LabelCount(x.Key.ToString(), x.Count)).ToList();

        return new AnalyticsDto(
            PremiumWritten: premiumWritten,
            ClaimsPaid: claimsPaid,
            LossRatio: premiumWritten > 0 ? (double)(claimsPaid / premiumWritten) : 0,
            PremiumByMonth: premiumByMonth,
            PoliciesByStatus: polByStatus,
            PoliciesByCoverage: polByCoverage,
            ClaimsByStatus: claimsByStatus);
    }
}

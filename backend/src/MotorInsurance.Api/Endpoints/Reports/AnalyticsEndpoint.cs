using System.Globalization;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Reports;

public record MonthPremium(string Month, decimal Premium);
public record LabelCount(string Label, int Count);

public record AnalyticsDto(
    decimal PremiumWritten,
    decimal ClaimsPaid,
    double LossRatio,
    IReadOnlyList<MonthPremium> PremiumByMonth,
    IReadOnlyList<LabelCount> PoliciesByStatus,
    IReadOnlyList<LabelCount> PoliciesByCoverage,
    IReadOnlyList<LabelCount> ClaimsByStatus);

/// <summary>GET /api/reports/analytics — aggregates for the reporting dashboard.</summary>
public class AnalyticsEndpoint : EndpointWithoutRequest<AnalyticsDto>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public AnalyticsEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("reports/analytics");
        Policies(PermissionPolicy.For(Perms.DashboardRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Premium written per month over a rolling 12-month window (zero-filled).
        var now = _clock.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
        var grouped = await _db.Policies.AsNoTracking()
            .Where(p => p.CreatedAt >= windowStart)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Premium = g.Sum(x => x.Premium) })
            .ToListAsync(ct);

        var premiumByMonth = new List<MonthPremium>(12);
        for (var i = 0; i < 12; i++)
        {
            var m = windowStart.AddMonths(i);
            var hit = grouped.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
            // Invariant culture → Gregorian "yyyy-MM" key (the FE converts to พ.ศ. for display).
            premiumByMonth.Add(new MonthPremium(m.ToString("yyyy-MM", CultureInfo.InvariantCulture), hit?.Premium ?? 0m));
        }

        var premiumWritten = await _db.Policies.AsNoTracking().SumAsync(p => p.Premium, ct);
        // Claims paid = settled outbound payouts (the money actually leaving for claims).
        var claimsPaid = await _db.Payments.AsNoTracking()
            .Where(p => p.Direction == PaymentDirection.Outbound && p.Status == PaymentStatus.Paid)
            .Select(p => (decimal?)p.Amount).SumAsync(ct) ?? 0m;

        // Group by converted-enum string columns in SQL, then label in memory.
        var polByStatus = (await _db.Policies.AsNoTracking()
            .GroupBy(p => p.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new LabelCount(x.Key.ToString(), x.Count)).ToList();
        var polByCoverage = (await _db.Policies.AsNoTracking()
            .GroupBy(p => p.CoverageType).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new LabelCount(x.Key.ToString(), x.Count)).ToList();
        var claimsByStatus = (await _db.Claims.AsNoTracking()
            .GroupBy(c => c.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .Select(x => new LabelCount(x.Key.ToString(), x.Count)).ToList();

        Response = new AnalyticsDto(
            PremiumWritten: premiumWritten,
            ClaimsPaid: claimsPaid,
            LossRatio: premiumWritten > 0 ? (double)(claimsPaid / premiumWritten) : 0,
            PremiumByMonth: premiumByMonth,
            PoliciesByStatus: polByStatus,
            PoliciesByCoverage: polByCoverage,
            ClaimsByStatus: claimsByStatus);
    }
}

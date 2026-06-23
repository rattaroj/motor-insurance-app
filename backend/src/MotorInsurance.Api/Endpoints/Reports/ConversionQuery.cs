using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;

namespace MotorInsurance.Api.Endpoints.Reports;

/// <summary>
/// Quote-to-bind conversion aggregation, shared by the JSON endpoint and its CSV export. A quotation
/// counts as "bound" once a <see cref="Domain.Entities.Policy"/> references it (<c>Policy.QuotationId</c>).
/// Figures are a cohort by quotation-creation date: when <paramref name="from"/>/<paramref name="to"/>
/// are supplied every figure is scoped to quotes created in that window; otherwise it is all-time with a
/// rolling 12-month trend. "Bound" is counted even when the policy was issued after the window — the
/// cohort is about whether a quote created in the period ever converted.
/// </summary>
public static class ConversionQuery
{
    public static async Task<ConversionDto> ComputeAsync(
        IAppDbContext db, IDateTimeProvider clock, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var hasRange = from is not null || to is not null;
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.Date);

        var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var fromDt = from?.ToDateTime(TimeOnly.MinValue) ?? thisMonth.AddMonths(-11);
        var toDt = to?.ToDateTime(TimeOnly.MaxValue) ?? thisMonth.AddMonths(1).AddTicks(-1);

        // Pull the quote cohort with, for each quote, the creation time of its binding policy (if any).
        var quotes = db.Quotations.AsNoTracking();
        if (hasRange) quotes = quotes.Where(q => q.CreatedAt >= fromDt && q.CreatedAt <= toDt);

        var rows = await quotes
            .Select(q => new
            {
                q.CoverageType,
                q.Premium,
                q.CreatedAt,
                q.ValidUntil,
                BoundAt = db.Policies
                    .Where(p => p.QuotationId == q.Id)
                    .Select(p => (DateTime?)p.CreatedAt)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var total = rows.Count;
        var bound = rows.Count(r => r.BoundAt is not null);
        var unbound = rows.Where(r => r.BoundAt is null).ToList();

        var quotedPremium = rows.Sum(r => r.Premium);
        var boundPremium = rows.Where(r => r.BoundAt is not null).Sum(r => r.Premium);

        // Average days from quote to bind (only over converted quotes; non-negative, guarded).
        var boundRows = rows.Where(r => r.BoundAt is not null).ToList();
        var avgDaysToBind = boundRows.Count == 0
            ? 0d
            : boundRows.Average(r => Math.Max(0d, (r.BoundAt!.Value - r.CreatedAt).TotalDays));

        // Per-coverage funnel.
        var byCoverage = rows
            .GroupBy(r => r.CoverageType)
            .Select(g =>
            {
                var q = g.Count();
                var b = g.Count(x => x.BoundAt is not null);
                return new ConversionByCoverage(g.Key.ToString(), q, b, q > 0 ? (double)b / q : 0);
            })
            .OrderByDescending(c => c.Quotes)
            .ToList();

        // Monthly trend (quotes vs bound by quote-creation month), zero-filled, capped at 36 months.
        var byMonthRaw = rows
            .GroupBy(r => new { r.CreatedAt.Year, r.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Quotes = g.Count(), Bound = g.Count(x => x.BoundAt is not null) })
            .ToList();

        var byMonth = new List<MonthConversion>();
        var cursor = new DateTime(fromDt.Year, fromDt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonth = new DateTime(toDt.Year, toDt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; cursor <= lastMonth && i < 36; cursor = cursor.AddMonths(1), i++)
        {
            var hit = byMonthRaw.FirstOrDefault(x => x.Year == cursor.Year && x.Month == cursor.Month);
            byMonth.Add(new MonthConversion(
                cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture), hit?.Quotes ?? 0, hit?.Bound ?? 0));
        }

        return new ConversionDto(
            TotalQuotes: total,
            BoundQuotes: bound,
            ConversionRate: total > 0 ? (double)bound / total : 0,
            OpenQuotes: unbound.Count(r => r.ValidUntil >= today),
            ExpiredUnbound: unbound.Count(r => r.ValidUntil < today),
            QuotedPremium: quotedPremium,
            BoundPremium: boundPremium,
            AvgDaysToBind: avgDaysToBind,
            ByCoverage: byCoverage,
            ByMonth: byMonth);
    }
}

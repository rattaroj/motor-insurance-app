using MotorInsurance.Api.Endpoints.Reports;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// Quote-to-bind conversion analytics: a quotation is "bound" once a policy references it
/// (Policy.QuotationId). Cohort is by quote-creation date; figures cover the funnel + per-coverage
/// breakdown + monthly trend. Mirrors the AnalyticsQuery aggregation shape.
/// </summary>
public class ConversionAnalyticsTests
{
    private static readonly DateTime Now = new(2026, 6, 23, 0, 0, 0, DateTimeKind.Utc);

    private static Quotation Quote(long id, DateTime createdAt, CoverageType cov = CoverageType.Type1,
        decimal premium = 10_000m, int validDays = 30) =>
        new()
        {
            Id = id, QuotationNo = $"QUO-2026-{id:D6}", CustomerId = 1, VehicleId = 1,
            CoverageType = cov, SumInsured = 500_000m, Premium = premium,
            CreatedAt = createdAt, ValidUntil = DateOnly.FromDateTime(createdAt.AddDays(validDays)),
        };

    private static Policy BoundTo(long id, long quotationId, DateTime createdAt) =>
        new()
        {
            Id = id, PolicyNo = $"POL-2026-{id:D6}", QuotationId = quotationId, CustomerId = 1, VehicleId = 1,
            Status = PolicyStatus.Issued, CoverageType = CoverageType.Type1, Premium = 10_000m, CreatedAt = createdAt,
        };

    [Fact]
    public async Task Computes_conversion_rate_and_bound_premium()
    {
        await using var db = InMemoryAppDb.New();
        db.Quotations.AddRange(
            Quote(1, Now.AddDays(-10), premium: 12_000m),
            Quote(2, Now.AddDays(-9), premium: 8_000m),
            Quote(3, Now.AddDays(-8), premium: 5_000m));     // stays unbound
        // Quotes 1 & 2 convert (bound 3 and 5 days after the quote).
        db.Policies.Add(BoundTo(100, 1, Now.AddDays(-7)));
        db.Policies.Add(BoundTo(101, 2, Now.AddDays(-4)));
        await db.SaveChangesAsync();

        var c = await ConversionQuery.ComputeAsync(db, new FixedClockProvider(Now), null, null, default);

        Assert.Equal(3, c.TotalQuotes);
        Assert.Equal(2, c.BoundQuotes);
        Assert.Equal(2d / 3d, c.ConversionRate, 3);
        Assert.Equal(25_000m, c.QuotedPremium);
        Assert.Equal(20_000m, c.BoundPremium);
        Assert.Equal(4d, c.AvgDaysToBind, 3);    // (3 + 5) / 2
    }

    [Fact]
    public async Task Splits_unbound_into_open_and_expired()
    {
        await using var db = InMemoryAppDb.New();
        db.Quotations.AddRange(
            Quote(1, Now.AddDays(-5), validDays: 30),     // still valid → open
            Quote(2, Now.AddDays(-40), validDays: 30));   // ValidUntil < today → expired-unbound
        await db.SaveChangesAsync();

        var c = await ConversionQuery.ComputeAsync(db, new FixedClockProvider(Now), null, null, default);

        Assert.Equal(0, c.BoundQuotes);
        Assert.Equal(1, c.OpenQuotes);
        Assert.Equal(1, c.ExpiredUnbound);
    }

    [Fact]
    public async Task Breaks_down_by_coverage()
    {
        await using var db = InMemoryAppDb.New();
        db.Quotations.AddRange(
            Quote(1, Now.AddDays(-6), CoverageType.Type1),
            Quote(2, Now.AddDays(-6), CoverageType.Type1),
            Quote(3, Now.AddDays(-6), CoverageType.Type3));
        db.Policies.Add(BoundTo(100, 1, Now.AddDays(-3)));   // one Type1 converts
        await db.SaveChangesAsync();

        var c = await ConversionQuery.ComputeAsync(db, new FixedClockProvider(Now), null, null, default);

        var type1 = Assert.Single(c.ByCoverage, x => x.Coverage == "Type1");
        Assert.Equal(2, type1.Quotes);
        Assert.Equal(1, type1.Bound);
        Assert.Equal(0.5, type1.Rate, 3);
        var type3 = Assert.Single(c.ByCoverage, x => x.Coverage == "Type3");
        Assert.Equal(0, type3.Bound);
    }

    [Fact]
    public async Task Range_scopes_the_cohort_by_quote_creation_date()
    {
        await using var db = InMemoryAppDb.New();
        db.Quotations.AddRange(
            Quote(1, new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc)),    // inside range
            Quote(2, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)));    // before range
        await db.SaveChangesAsync();

        var from = new DateOnly(2026, 3, 1);
        var to = new DateOnly(2026, 3, 31);
        var c = await ConversionQuery.ComputeAsync(db, new FixedClockProvider(Now), from, to, default);

        Assert.Equal(1, c.TotalQuotes);   // only the March quote is in the cohort
    }

    [Fact]
    public async Task Counts_a_quote_bound_after_the_window_as_converted()
    {
        // Cohort = quotes created in March; the binding policy is issued in April (after the window).
        await using var db = InMemoryAppDb.New();
        db.Quotations.Add(Quote(1, new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)));
        db.Policies.Add(BoundTo(100, 1, new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var c = await ConversionQuery.ComputeAsync(
            db, new FixedClockProvider(Now), new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), default);

        Assert.Equal(1, c.TotalQuotes);
        Assert.Equal(1, c.BoundQuotes);
    }
}

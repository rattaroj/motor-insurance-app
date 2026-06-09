using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Endpoints.Policies;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// Mid-term coverage endorsement: re-rates the premium and raises the pro-rata additional
/// (inbound) or return (outbound) premium as a pending payment. (F6)
/// </summary>
public class CoverageEndorsementTests
{
    // Active Type1 policy, 500k sum @ 22,500 premium, full one-year term.
    private static async Task<InMemoryAppDb> DbWithActivePolicyAsync()
    {
        var db = InMemoryAppDb.New();
        db.Policies.Add(new Policy
        {
            Id = 1,
            PolicyNo = "POL-TEST-0001",
            CustomerId = 1,
            VehicleId = 1,
            Status = PolicyStatus.Active,
            CoverageType = CoverageType.Type1,
            SumInsured = 500_000m,
            BasePremium = 22_500m,
            Premium = 22_500m,
            NcbPercent = 0,
            Deductible = 0,
            EffectiveDate = new DateOnly(2025, 6, 15),
            ExpiryDate = new DateOnly(2026, 6, 15),
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static CoverageEndorsementEndpoint Sut(InMemoryAppDb db) =>
        new(db, new FakeDocNoGenerator(), new FixedClockProvider());

    [Fact]
    public async Task Increasing_sum_insured_raises_inbound_additional_premium()
    {
        await using var db = await DbWithActivePolicyAsync();

        // Effective on the policy start → pro-rata factor = 1, so delta = full premium difference.
        var res = await Sut(db).EndorseCoverageAsync(1,
            new CoverageEndorsementRequest(null, 1_000_000m, null, new DateOnly(2025, 6, 15), null), default);

        Assert.Equal(45_000m, res.NewPremium);     // 1,000,000 * 0.045
        Assert.Equal(22_500m, res.PremiumDelta);   // 45,000 - 22,500
        Assert.NotNull(res.PaymentNo);

        var policy = await db.Policies.FindAsync(1L);
        Assert.Equal(45_000m, policy!.Premium);
        Assert.Equal(1_000_000m, policy.SumInsured);

        var payment = await db.Payments.SingleAsync();
        Assert.Equal(PaymentDirection.Inbound, payment.Direction);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(22_500m, payment.Amount);
    }

    [Fact]
    public async Task Decreasing_sum_insured_raises_outbound_return_premium()
    {
        await using var db = await DbWithActivePolicyAsync();

        var res = await Sut(db).EndorseCoverageAsync(1,
            new CoverageEndorsementRequest(null, 200_000m, null, new DateOnly(2025, 6, 15), null), default);

        Assert.Equal(9_000m, res.NewPremium);        // 200,000 * 0.045
        Assert.Equal(-13_500m, res.PremiumDelta);    // 9,000 - 22,500

        var payment = await db.Payments.SingleAsync();
        Assert.Equal(PaymentDirection.Outbound, payment.Direction);
        Assert.Equal(13_500m, payment.Amount);       // absolute value
    }

    [Fact]
    public async Task Mid_term_endorsement_prorates_the_delta()
    {
        await using var db = await DbWithActivePolicyAsync();

        // Half-way through the term → delta is a fraction of the full 22,500 difference.
        var res = await Sut(db).EndorseCoverageAsync(1,
            new CoverageEndorsementRequest(null, 1_000_000m, null, new DateOnly(2025, 12, 15), null), default);

        Assert.True(res.PremiumDelta > 0 && res.PremiumDelta < 22_500m);
    }

    [Fact]
    public async Task Rejects_when_policy_not_active()
    {
        await using var db = await DbWithActivePolicyAsync();
        var policy = await db.Policies.FindAsync(1L);
        policy!.Status = PolicyStatus.Issued;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => Sut(db).EndorseCoverageAsync(1,
            new CoverageEndorsementRequest(null, 1_000_000m, null, new DateOnly(2025, 6, 15), null), default));
    }

    [Fact]
    public async Task Rejects_when_nothing_changes()
    {
        await using var db = await DbWithActivePolicyAsync();

        await Assert.ThrowsAsync<ConflictException>(() => Sut(db).EndorseCoverageAsync(1,
            new CoverageEndorsementRequest(null, 500_000m, null, new DateOnly(2025, 6, 15), null), default));
    }
}

using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>Pure unit tests for the rating engine (no DB). Numbers track the placeholder factors.</summary>
public class PremiumCalculatorTests
{
    private static PremiumInput Input(
        CoverageType coverage = CoverageType.Type1, decimal sumInsured = 1_000_000m,
        int ageYears = 0, int ncb = 0, decimal deductible = 0, params decimal[] riders) =>
        new(coverage, sumInsured, ageYears, ncb, deductible, riders);

    [Fact]
    public void Base_premium_is_sum_insured_times_coverage_rate()
    {
        var b = PremiumCalculator.Rate(Input(CoverageType.Type1, 1_000_000m));
        Assert.Equal(45_000m, b.BasePremium);   // 1,000,000 * 0.045
        Assert.Equal(0m, b.VehicleAgeLoading);
        Assert.Equal(45_000m, b.NetPremium);
    }

    [Theory]
    [InlineData(CoverageType.Type1, 0.045)]
    [InlineData(CoverageType.Type2Plus, 0.030)]
    [InlineData(CoverageType.Type3Plus, 0.022)]
    [InlineData(CoverageType.Type3, 0.015)]
    public void Coverage_rate_table(CoverageType coverage, double rate)
    {
        var b = PremiumCalculator.Rate(Input(coverage, 1_000_000m));
        Assert.Equal(Math.Round(1_000_000m * (decimal)rate, 2), b.BasePremium);
    }

    [Theory]
    [InlineData(3, 0.00)]    // <= 5y: no loading
    [InlineData(8, 0.05)]    // 6-10y
    [InlineData(15, 0.10)]   // > 10y
    public void Vehicle_age_adds_loading(int age, double loadingRate)
    {
        var b = PremiumCalculator.Rate(Input(CoverageType.Type1, 1_000_000m, ageYears: age));
        Assert.Equal(Math.Round(45_000m * (decimal)loadingRate, 2), b.VehicleAgeLoading);
        Assert.Equal(45_000m + b.VehicleAgeLoading, b.NetPremium);
    }

    [Fact]
    public void Ncb_discounts_base_plus_loading()
    {
        // 50% NCB on (45,000 base + 0 loading) = 22,500 off
        var b = PremiumCalculator.Rate(Input(CoverageType.Type1, 1_000_000m, ncb: 50));
        Assert.Equal(22_500m, b.NcbDiscount);
        Assert.Equal(22_500m, b.NetPremium);
    }

    [Fact]
    public void Ncb_is_snapped_to_allowed_step()
    {
        // 37 -> 30% step
        var b = PremiumCalculator.Rate(Input(CoverageType.Type1, 1_000_000m, ncb: 37));
        Assert.Equal(Math.Round(45_000m * 0.30m, 2), b.NcbDiscount);
    }

    [Fact]
    public void Deductible_relief_is_capped_at_20pct_of_base()
    {
        // deductible 100,000 * 0.5 = 50,000 but capped at 45,000 * 0.20 = 9,000
        var b = PremiumCalculator.Rate(Input(CoverageType.Type1, 1_000_000m, deductible: 100_000m));
        Assert.Equal(9_000m, b.DeductibleDiscount);
    }

    [Fact]
    public void Riders_add_their_flat_premiums()
    {
        var b = PremiumCalculator.Rate(Input(CoverageType.Type1, 1_000_000m, 0, 0, 0, 1_200m, 800m));
        Assert.Equal(2_000m, b.RidersTotal);
        Assert.Equal(47_000m, b.NetPremium);   // 45,000 + 2,000
    }

    [Fact]
    public void Net_premium_never_negative()
    {
        // tiny base, full NCB + deductible relief -> floored at 0
        var b = PremiumCalculator.Rate(Input(CoverageType.Type3, 10_000m, ncb: 50, deductible: 100_000m));
        Assert.True(b.NetPremium >= 0m);
    }

    [Fact]
    public void Combined_breakdown_sums_to_net()
    {
        var b = PremiumCalculator.Rate(Input(CoverageType.Type1, 1_000_000m, 8, 30, 5_000m, 1_500m));
        var expected = b.BasePremium + b.VehicleAgeLoading - b.NcbDiscount - b.DeductibleDiscount + b.RidersTotal;
        Assert.Equal(Math.Round(expected, 2), b.NetPremium);
    }

    [Fact]
    public void Ncb_steps_up_after_claim_free_year_and_caps_at_50()
    {
        Assert.Equal(20, PremiumCalculator.StepUpNcb(0));
        Assert.Equal(30, PremiumCalculator.StepUpNcb(20));
        Assert.Equal(50, PremiumCalculator.StepUpNcb(40));
        Assert.Equal(50, PremiumCalculator.StepUpNcb(50));
    }

    [Fact]
    public void Ncb_resets_to_zero_after_a_claim()
    {
        Assert.Equal(0, PremiumCalculator.StepDownNcb(50));
    }
}

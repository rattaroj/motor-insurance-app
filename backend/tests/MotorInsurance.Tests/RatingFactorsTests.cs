using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// DB-backed rating: PremiumRatingService resolves the configurable, effective-dated factors
/// (premium_rate / age_loading_band / rating_setting) and falls back to the built-in defaults
/// when a table is empty. (F7)
/// </summary>
public class RatingFactorsTests
{
    private static async Task<decimal> BaseAsync(
        InMemoryAppDb db, int asOfYear, CoverageType coverage = CoverageType.Type1,
        decimal sum = 1_000_000m, long vehicleId = 0, int ncb = 0, decimal deductible = 0)
    {
        var r = await PremiumRatingService.RateAsync(db, asOfYear, vehicleId, coverage, sum, ncb, deductible, null, default);
        return r.Breakdown.BasePremium;
    }

    [Fact]
    public async Task Empty_tables_fall_back_to_builtin_defaults()
    {
        await using var db = InMemoryAppDb.New();
        var b = await BaseAsync(db, 2026);
        Assert.Equal(45_000m, b);   // built-in Type1 rate 0.045
    }

    [Fact]
    public async Task Configured_coverage_rate_overrides_the_default()
    {
        await using var db = InMemoryAppDb.New();
        db.PremiumRates.Add(new PremiumRate { Coverage = CoverageType.Type1, Rate = 0.05m, EffectiveDate = new DateOnly(2000, 1, 1) });
        await db.SaveChangesAsync();

        Assert.Equal(50_000m, await BaseAsync(db, 2026));   // 1,000,000 * 0.05
    }

    [Fact]
    public async Task Rate_selection_is_effective_dated_by_as_of_year()
    {
        await using var db = InMemoryAppDb.New();
        db.PremiumRates.Add(new PremiumRate { Coverage = CoverageType.Type1, Rate = 0.045m, EffectiveDate = new DateOnly(2000, 1, 1) });
        db.PremiumRates.Add(new PremiumRate { Coverage = CoverageType.Type1, Rate = 0.050m, EffectiveDate = new DateOnly(2026, 1, 1) });
        await db.SaveChangesAsync();

        Assert.Equal(50_000m, await BaseAsync(db, 2026));   // picks the 2026 rate
        Assert.Equal(45_000m, await BaseAsync(db, 2025));   // 2026 rate not yet effective
    }

    [Fact]
    public async Task Configured_deductible_relief_overrides_the_default()
    {
        await using var db = InMemoryAppDb.New();
        db.RatingSettings.Add(new RatingSetting { Code = "DEDUCTIBLE_RELIEF_RATE", Value = 1.0m });
        db.RatingSettings.Add(new RatingSetting { Code = "DEDUCTIBLE_RELIEF_CAP", Value = 0.5m });
        await db.SaveChangesAsync();

        var r = await PremiumRatingService.RateAsync(
            db, 2026, 0, CoverageType.Type1, 1_000_000m, 0, 100_000m, null, default);

        // min(100,000 * 1.0, 45,000 * 0.5) = 22,500 (default would be 9,000)
        Assert.Equal(22_500m, r.Breakdown.DeductibleDiscount);
    }

    [Fact]
    public async Task Configured_age_bands_override_the_default()
    {
        await using var db = InMemoryAppDb.New();
        db.VehicleModelYears.Add(new VehicleModelYear { Id = 10, SubmodelId = 1, Year = 2018 });
        db.Vehicles.Add(new Vehicle { Id = 1, CustomerId = 1, RegistrationNo = "กก1", Province = "กทม", ModelYearId = 10 });
        db.AgeLoadingBands.Add(new AgeLoadingBand { MaxAge = 5, Surcharge = 0m, EffectiveDate = new DateOnly(2000, 1, 1) });
        db.AgeLoadingBands.Add(new AgeLoadingBand { MaxAge = null, Surcharge = 0.30m, EffectiveDate = new DateOnly(2000, 1, 1) });
        await db.SaveChangesAsync();

        // As-of 2026, vehicle year 2018 → age 8 → open-ended band 0.30.
        var r = await PremiumRatingService.RateAsync(
            db, 2026, 1, CoverageType.Type1, 1_000_000m, 0, 0, null, default);

        Assert.Equal(13_500m, r.Breakdown.VehicleAgeLoading);   // 45,000 * 0.30 (default would be 0.05)
    }
}

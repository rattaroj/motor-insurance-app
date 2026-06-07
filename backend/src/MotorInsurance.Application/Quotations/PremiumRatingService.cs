using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Quotations;

public record RatingResult(PremiumBreakdown Breakdown, IReadOnlyList<long> RiderIds);

/// <summary>
/// Bridges the DB to the pure <see cref="PremiumCalculator"/>: resolves vehicle age (from the
/// model year) and the selected riders' premiums, then rates. Shared by quotation create,
/// the live preview, and renewal (CLAUDE.md: shared logic lives in Application as a plain helper).
/// </summary>
public static class PremiumRatingService
{
    public static async Task<RatingResult> RateAsync(
        IAppDbContext db, int asOfYear, long vehicleId,
        CoverageType coverage, decimal sumInsured, int ncbPercent, decimal deductible,
        IReadOnlyList<long>? riderIds, CancellationToken ct)
    {
        var modelYear = await db.Vehicles
            .Where(v => v.Id == vehicleId)
            .Select(v => (int?)v.ModelYear.Year)
            .FirstOrDefaultAsync(ct);
        var ageYears = modelYear is null ? 0 : Math.Max(0, asOfYear - modelYear.Value);

        var ids = (riderIds ?? Array.Empty<long>()).Distinct().ToList();
        var riders = ids.Count == 0
            ? new List<Rider>()
            : await db.Riders.Where(r => ids.Contains(r.Id)).ToListAsync(ct);
        if (riders.Count != ids.Count)
            throw new NotFoundException(nameof(Rider), string.Join(",", ids.Except(riders.Select(r => r.Id))));

        // Resolve the configurable, effective-dated rating factors (fall back to built-in defaults
        // per-part when a table is empty). Cut-off = end of the as-of year.
        var factors = await ResolveFactorsAsync(db, asOfYear, coverage, ct);

        var breakdown = PremiumCalculator.Rate(new PremiumInput(
            coverage, sumInsured, ageYears,
            PremiumCalculator.NormalizeNcb(ncbPercent), deductible,
            riders.Select(r => r.Premium).ToList()), factors);

        return new RatingResult(breakdown, ids);
    }

    /// <summary>
    /// Builds <see cref="RateFactors"/> from the configurable tables as of <paramref name="asOfYear"/>:
    /// the latest effective coverage rate, the newest effective age-loading band set, and the
    /// deductible-relief settings — each falling back to the built-in default when not configured.
    /// </summary>
    private static async Task<RateFactors> ResolveFactorsAsync(
        IAppDbContext db, int asOfYear, CoverageType coverage, CancellationToken ct)
    {
        var defaults = PremiumCalculator.DefaultFactors(coverage);
        var cutoff = new DateOnly(asOfYear, 12, 31);

        var coverageRate = await db.PremiumRates
            .Where(r => r.Coverage == coverage && r.EffectiveDate <= cutoff)
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => (decimal?)r.Rate)
            .FirstOrDefaultAsync(ct);

        // Newest effective band set (all bands of a version share one EffectiveDate).
        var bandDate = await db.AgeLoadingBands
            .Where(b => b.EffectiveDate <= cutoff)
            .MaxAsync(b => (DateOnly?)b.EffectiveDate, ct);
        IReadOnlyList<(int? MaxAge, decimal Surcharge)>? bands = null;
        if (bandDate is not null)
            bands = (await db.AgeLoadingBands
                    .Where(b => b.EffectiveDate == bandDate)
                    .OrderBy(b => b.MaxAge == null).ThenBy(b => b.MaxAge)
                    .Select(b => new { b.MaxAge, b.Surcharge })
                    .ToListAsync(ct))
                .Select(b => (b.MaxAge, b.Surcharge)).ToList();

        var settings = await db.RatingSettings
            .ToDictionaryAsync(s => s.Code, s => s.Value, ct);

        return new RateFactors(
            coverageRate ?? defaults.CoverageRate,
            bands ?? defaults.AgeBands,
            settings.TryGetValue("DEDUCTIBLE_RELIEF_RATE", out var rr) ? rr : defaults.DeductibleReliefRate,
            settings.TryGetValue("DEDUCTIBLE_RELIEF_CAP", out var cap) ? cap : defaults.DeductibleReliefCap);
    }
}

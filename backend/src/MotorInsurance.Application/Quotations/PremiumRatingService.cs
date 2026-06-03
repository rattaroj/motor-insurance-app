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

        var breakdown = PremiumCalculator.Rate(new PremiumInput(
            coverage, sumInsured, ageYears,
            PremiumCalculator.NormalizeNcb(ncbPercent), deductible,
            riders.Select(r => r.Premium).ToList()));

        return new RatingResult(breakdown, ids);
    }
}

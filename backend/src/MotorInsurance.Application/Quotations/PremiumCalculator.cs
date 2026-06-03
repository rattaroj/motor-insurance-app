using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Quotations;

/// <summary>Inputs for premium rating. The caller resolves vehicle age and rider premiums.</summary>
public record PremiumInput(
    CoverageType Coverage,
    decimal SumInsured,
    int VehicleAgeYears,
    int NcbPercent,
    decimal Deductible,
    IReadOnlyList<decimal> RiderPremiums);

/// <summary>The rated premium broken down by factor, so the UI/documents can show its origin.</summary>
public record PremiumBreakdown(
    decimal BasePremium,
    decimal VehicleAgeLoading,
    decimal NcbDiscount,
    decimal DeductibleDiscount,
    decimal RidersTotal,
    decimal NetPremium);

/// <summary>
/// Simplified premium rating. ALL rates/factors below are illustrative placeholders, NOT real
/// actuarial tariffs — a real system plugs an actuarial engine in here. Pure/stateless (no DB),
/// so it is trivially unit-testable.
/// </summary>
public static class PremiumCalculator
{
    /// <summary>Allowed no-claim-bonus discount steps (Thai OIC convention).</summary>
    public static readonly IReadOnlyList<int> NcbSteps = new[] { 0, 20, 30, 40, 50 };

    private const decimal DeductibleReliefRate = 0.5m;   // half the deductible comes off the premium…
    private const decimal DeductibleReliefCap = 0.20m;   // …capped at 20% of the base premium.

    private static decimal CoverageRate(CoverageType coverage) => coverage switch
    {
        CoverageType.Type1     => 0.045m,
        CoverageType.Type2Plus => 0.030m,
        CoverageType.Type3Plus => 0.022m,
        CoverageType.Type3     => 0.015m,
        _ => 0.045m,
    };

    // Older vehicles carry a surcharge on the base premium.
    private static decimal AgeLoadingRate(int ageYears) => ageYears switch
    {
        <= 5  => 0.00m,
        <= 10 => 0.05m,
        _     => 0.10m,
    };

    /// <summary>Full breakdown of the premium for the given inputs.</summary>
    public static PremiumBreakdown Rate(PremiumInput input)
    {
        var basePremium = Math.Round(input.SumInsured * CoverageRate(input.Coverage), 2);
        var ageLoading = Math.Round(basePremium * AgeLoadingRate(input.VehicleAgeYears), 2);

        var ncbPercent = NormalizeNcb(input.NcbPercent);
        var ncbDiscount = Math.Round((basePremium + ageLoading) * ncbPercent / 100m, 2);

        var deductible = Math.Max(0m, input.Deductible);
        var deductibleDiscount = Math.Round(
            Math.Min(deductible * DeductibleReliefRate, basePremium * DeductibleReliefCap), 2);

        var ridersTotal = input.RiderPremiums?.Sum() ?? 0m;

        var net = basePremium + ageLoading - ncbDiscount - deductibleDiscount + ridersTotal;
        net = Math.Max(0m, Math.Round(net, 2));

        return new PremiumBreakdown(basePremium, ageLoading, ncbDiscount, deductibleDiscount, ridersTotal, net);
    }

    /// <summary>Convenience: the net premium only (used where the breakdown isn't needed).</summary>
    public static decimal Calculate(PremiumInput input) => Rate(input).NetPremium;

    /// <summary>
    /// Backward-compatible base rating (no NCB/deductible/age/riders) — equals the base premium.
    /// </summary>
    public static decimal Calculate(CoverageType coverage, decimal sumInsured) =>
        Rate(new PremiumInput(coverage, sumInsured, 0, 0, 0, Array.Empty<decimal>())).NetPremium;

    /// <summary>Snap an NCB percent to the nearest allowed step at or below it (defensive).</summary>
    public static int NormalizeNcb(int ncbPercent)
    {
        var allowed = 0;
        foreach (var step in NcbSteps)
            if (ncbPercent >= step) allowed = step;
        return allowed;
    }

    /// <summary>Next NCB step up after a claim-free year (caps at the top step).</summary>
    public static int StepUpNcb(int ncbPercent)
    {
        var current = NormalizeNcb(ncbPercent);
        foreach (var step in NcbSteps)
            if (step > current) return step;
        return current;
    }

    /// <summary>After a year with claims, NCB resets to 0 (simplified rule).</summary>
    public static int StepDownNcb(int ncbPercent) => 0;
}

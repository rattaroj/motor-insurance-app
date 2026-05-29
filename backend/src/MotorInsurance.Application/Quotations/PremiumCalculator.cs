using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Quotations;

/// <summary>Simplified premium rating. Real systems plug in an actuarial engine here.</summary>
public static class PremiumCalculator
{
    public static decimal Calculate(CoverageType coverage, decimal sumInsured)
    {
        var rate = coverage switch
        {
            CoverageType.Type1     => 0.045m,
            CoverageType.Type2Plus => 0.030m,
            CoverageType.Type3Plus => 0.022m,
            CoverageType.Type3     => 0.015m,
            _ => 0.045m
        };
        return Math.Round(sumInsured * rate, 2);
    }
}

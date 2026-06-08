namespace MotorInsurance.Application.Claims;

/// <summary>One risk signal raised on a claim. <see cref="Severity"/> is "warn" or "info".</summary>
public record ClaimRiskFlag(string Code, string Label, string Severity);

/// <summary>
/// Heuristic claim-risk signals to help a reviewer triage suspicious claims — NOT an automated
/// decision. Pure function over scalars so it is unit-testable without the DB (like
/// <see cref="Quotations.PremiumCalculator"/>); the endpoint gathers the inputs and calls Evaluate.
/// </summary>
public static class ClaimRiskRules
{
    /// <summary>An incident this soon after the policy takes effect is worth a second look.</summary>
    public const int EarlyClaimDays = 14;

    /// <summary>Claimed amount at/above this share of the sum insured is a high-severity claim.</summary>
    public const decimal HighRatioThreshold = 0.8m;

    /// <summary>This many claims (including the current one) on a single policy is unusual.</summary>
    public const int FrequentClaimsThreshold = 3;

    public static IReadOnlyList<ClaimRiskFlag> Evaluate(
        DateOnly incidentDate, DateOnly? policyEffectiveDate,
        decimal claimedAmount, decimal sumInsured, int priorClaimsOnPolicy)
    {
        var flags = new List<ClaimRiskFlag>();

        if (policyEffectiveDate is { } eff)
        {
            var daysAfter = incidentDate.DayNumber - eff.DayNumber;
            if (daysAfter is >= 0 and <= EarlyClaimDays)
                flags.Add(new ClaimRiskFlag(
                    "EARLY_CLAIM", $"เกิดเหตุภายใน {EarlyClaimDays} วันหลังกรมธรรม์มีผล", "warn"));
        }

        if (sumInsured > 0 && claimedAmount >= sumInsured * HighRatioThreshold)
            flags.Add(new ClaimRiskFlag("HIGH_RATIO", "ยอดเรียกร้อง ≥ 80% ของทุนประกัน", "warn"));

        var totalClaims = priorClaimsOnPolicy + 1;
        if (totalClaims >= FrequentClaimsThreshold)
            flags.Add(new ClaimRiskFlag("FREQUENT", $"กรมธรรม์นี้มีเคลม {totalClaims} ครั้ง", "info"));

        return flags;
    }
}

using MotorInsurance.Application.Claims;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>Pure unit tests for the claim-risk heuristics (no DB).</summary>
public class ClaimRiskRulesTests
{
    private static readonly DateOnly Effective = new(2026, 1, 1);

    [Fact]
    public void Flags_an_early_claim_within_the_window()
    {
        var flags = ClaimRiskRules.Evaluate(
            incidentDate: Effective.AddDays(10), policyEffectiveDate: Effective,
            claimedAmount: 1_000m, sumInsured: 1_000_000m, priorClaimsOnPolicy: 0);

        Assert.Contains(flags, f => f.Code == "EARLY_CLAIM");
    }

    [Fact]
    public void Does_not_flag_an_incident_well_after_inception()
    {
        var flags = ClaimRiskRules.Evaluate(
            incidentDate: Effective.AddDays(120), policyEffectiveDate: Effective,
            claimedAmount: 1_000m, sumInsured: 1_000_000m, priorClaimsOnPolicy: 0);

        Assert.DoesNotContain(flags, f => f.Code == "EARLY_CLAIM");
    }

    [Fact]
    public void Flags_a_high_claim_to_sum_insured_ratio()
    {
        var flags = ClaimRiskRules.Evaluate(
            incidentDate: Effective.AddDays(200), policyEffectiveDate: Effective,
            claimedAmount: 850_000m, sumInsured: 1_000_000m, priorClaimsOnPolicy: 0);

        Assert.Contains(flags, f => f.Code == "HIGH_RATIO");
    }

    [Fact]
    public void Flags_frequent_claims_counting_the_current_one()
    {
        // 2 prior + this one = 3 → at the threshold.
        var flags = ClaimRiskRules.Evaluate(
            incidentDate: Effective.AddDays(200), policyEffectiveDate: Effective,
            claimedAmount: 1_000m, sumInsured: 1_000_000m, priorClaimsOnPolicy: 2);

        Assert.Contains(flags, f => f.Code == "FREQUENT");
    }

    [Fact]
    public void Clean_claim_raises_no_flags()
    {
        var flags = ClaimRiskRules.Evaluate(
            incidentDate: Effective.AddDays(200), policyEffectiveDate: Effective,
            claimedAmount: 50_000m, sumInsured: 1_000_000m, priorClaimsOnPolicy: 0);

        Assert.Empty(flags);
    }
}

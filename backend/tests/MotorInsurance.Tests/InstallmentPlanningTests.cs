using MotorInsurance.Application.Policies;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>Pure unit tests for the installment schedule builder (no DB).</summary>
public class InstallmentPlanningTests
{
    private static readonly DateOnly Start = new(2026, 6, 1);

    [Fact]
    public void Splits_premium_evenly_and_adds_fee_to_the_first()
    {
        var s = InstallmentPlanning.BuildSchedule(premium: 9_000m, count: 3, fee: 300m, Start);

        Assert.Equal(3, s.Count);
        Assert.Equal(3_300m, s[0].Amount);   // 3,000 + 300 fee
        Assert.Equal(3_000m, s[1].Amount);
        Assert.Equal(3_000m, s[2].Amount);
    }

    [Fact]
    public void Parts_plus_fee_always_sum_to_premium_plus_fee_even_with_rounding()
    {
        // 10,000 / 3 = 3,333.33 each; the last installment absorbs the remainder.
        var s = InstallmentPlanning.BuildSchedule(premium: 10_000m, count: 3, fee: 300m, Start);

        Assert.Equal(10_300m, s.Sum(x => x.Amount));
        Assert.Equal(3_333.34m, s[2].Amount);   // 10,000 - 3,333.33 - 3,333.33
    }

    [Fact]
    public void Due_dates_step_by_frequency_from_the_start()
    {
        var s = InstallmentPlanning.BuildSchedule(premium: 9_000m, count: 3, fee: 0m, Start);

        Assert.Equal(Start, s[0].DueDate);
        Assert.Equal(Start.AddDays(InstallmentPlanning.FrequencyDays), s[1].DueDate);
        Assert.Equal(Start.AddDays(2 * InstallmentPlanning.FrequencyDays), s[2].DueDate);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void IsInstallment_is_true_only_for_two_or_more(int? count, bool expected)
        => Assert.Equal(expected, InstallmentPlanning.IsInstallment(count));
}

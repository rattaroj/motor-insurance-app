namespace MotorInsurance.Application.Policies;

/// <summary>
/// Builds an installment schedule from an annual premium: an equal split across N installments
/// (the last absorbs the rounding remainder), a flat financing <see cref="FlatFee"/> added to the
/// first (down) payment, the first due on the start date and the rest every <see cref="FrequencyDays"/>.
/// Pure functions so the schedule is unit-testable without the DB.
/// </summary>
public static class InstallmentPlanning
{
    public const int FrequencyDays = 30;
    public const int MaxInstallments = 6;

    /// <summary>Flat financing fee charged once per plan (added to the down payment).</summary>
    public const decimal FlatFee = 300m;

    public record Installment(int Seq, decimal Amount, DateOnly DueDate);

    /// <summary>True when the requested count means "pay in installments" (2..Max).</summary>
    public static bool IsInstallment(int? count) => count is >= 2;

    public static IReadOnlyList<Installment> BuildSchedule(
        decimal premium, int count, decimal fee, DateOnly startDate)
    {
        var per = Math.Round(premium / count, 2);
        var list = new List<Installment>(count);
        var running = 0m;
        for (var i = 1; i <= count; i++)
        {
            // The final installment takes whatever rounding left over so the parts sum to `premium`.
            var principal = i < count ? per : premium - running;
            running += principal;
            var amount = principal + (i == 1 ? fee : 0m);
            list.Add(new Installment(i, amount, startDate.AddDays((i - 1) * FrequencyDays)));
        }
        return list;
    }
}

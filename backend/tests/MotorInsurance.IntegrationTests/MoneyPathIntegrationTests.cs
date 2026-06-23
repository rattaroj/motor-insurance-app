using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Payments;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;
using MotorInsurance.Infrastructure;
using Xunit;

namespace MotorInsurance.IntegrationTests;

/// <summary>
/// The money-path against a real SQL Server: settle/cancel side-effects on the system-versioned
/// (temporal) policy/claim tables, plus rowversion optimistic concurrency — none of which EF InMemory
/// can model. Each test seeds its own graph under a unique tag so they can share the one database.
/// </summary>
[Collection("sqlserver")]
public class MoneyPathIntegrationTests
{
    private readonly SqlServerFixture _fx;
    public MoneyPathIntegrationTests(SqlServerFixture fx) => _fx = fx;

    [SkippableFact]
    public async Task Settling_the_premium_activates_the_policy_and_records_the_transition_in_history()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);
        await using var db = _fx.NewContext();
        var (policyId, paymentId) = await _fx.SeedIssuedPolicyWithPremiumAsync(db, "actv");

        await PaymentSettlement.SettleAsync(db, new TestClock(), paymentId, "TXN-ACTIVATE", default);

        // Current state: policy Active, payment Paid.
        await using (var verify = _fx.NewContext())
        {
            Assert.Equal(PolicyStatus.Active, (await verify.Policies.FindAsync(policyId))!.Status);
            var paid = await verify.Payments.FindAsync(paymentId);
            Assert.Equal(PaymentStatus.Paid, paid!.Status);
            Assert.Equal("TXN-ACTIVATE", paid.ReferenceNo);
            Assert.NotNull(paid.PaidAt);
        }

        // Temporal history captured the Issued → Active transition.
        var history = await new PolicyHistoryReader(_fx.NewContext()).GetHistoryAsync(policyId);
        var statuses = history.Select(h => h.Status).ToList();
        Assert.Contains("Issued", statuses);
        Assert.Contains("Active", statuses);
        Assert.True(statuses.IndexOf("Issued") < statuses.IndexOf("Active"), "Issued must precede Active in history");
    }

    [SkippableFact]
    public async Task Settling_the_payout_moves_the_claim_to_paid_and_records_it_in_history()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);
        await using var db = _fx.NewContext();
        var (_, claimId, paymentId) = await _fx.SeedApprovedClaimWithPayoutAsync(db, "pyot");

        await PaymentSettlement.SettleAsync(db, new TestClock(), paymentId, "TXN-PAYOUT", default);

        await using (var verify = _fx.NewContext())
            Assert.Equal(ClaimStatus.Paid, (await verify.Claims.FindAsync(claimId))!.Status);

        var history = await new ClaimHistoryReader(_fx.NewContext()).GetHistoryAsync(claimId);
        var statuses = history.Select(h => h.Status).ToList();
        Assert.Contains("Approved", statuses);
        Assert.Contains("Paid", statuses);
        Assert.True(statuses.IndexOf("Approved") < statuses.IndexOf("Paid"), "Approved must precede Paid in history");
    }

    [SkippableFact]
    public async Task Two_concurrent_settlements_of_the_same_payment_collide_on_rowversion()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);
        long paymentId;
        await using (var seed = _fx.NewContext())
            (_, paymentId) = await _fx.SeedIssuedPolicyWithPremiumAsync(seed, "race");

        // Two contexts load the same pending payment before either commits.
        await using var ctx1 = _fx.NewContext();
        await using var ctx2 = _fx.NewContext();
        var p1 = await ctx1.Payments.Include(p => p.Policy).FirstAsync(p => p.Id == paymentId);
        var p2 = await ctx2.Payments.Include(p => p.Policy).FirstAsync(p => p.Id == paymentId);

        PaymentSettlement.Apply(p1, DateTime.UtcNow, "TXN-WIN");
        await ctx1.SaveChangesAsync();   // first writer wins, bumps row_version

        PaymentSettlement.Apply(p2, DateTime.UtcNow, "TXN-LOSE");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync());

        // The losing reference never landed.
        await using var verify = _fx.NewContext();
        Assert.Equal("TXN-WIN", (await verify.Payments.FindAsync(paymentId))!.ReferenceNo);
    }

    [SkippableFact]
    public async Task Cancelling_an_active_policy_versions_the_row_into_history()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);
        await using var db = _fx.NewContext();
        var (policyId, paymentId) = await _fx.SeedIssuedPolicyWithPremiumAsync(db, "cncl");

        // Drive it through the lifecycle: Issued → Active (settle) → Cancelled.
        await PaymentSettlement.SettleAsync(db, new TestClock(), paymentId, "TXN-1", default);

        await using (var cancelCtx = _fx.NewContext())
        {
            var policy = await cancelCtx.Policies.FirstAsync(p => p.Id == policyId);
            PolicyStateMachine.EnsureTransition(policy.Status, PolicyStatus.Cancelled);
            policy.Status = PolicyStatus.Cancelled;
            await cancelCtx.SaveChangesAsync();
        }

        await using (var verify = _fx.NewContext())
            Assert.Equal(PolicyStatus.Cancelled, (await verify.Policies.FindAsync(policyId))!.Status);

        var statuses = (await new PolicyHistoryReader(_fx.NewContext()).GetHistoryAsync(policyId))
            .Select(h => h.Status).ToList();
        Assert.Equal(new[] { "Issued", "Active", "Cancelled" }, statuses);
    }
}

using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Payments;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// The shared settle hub (used by the manual settle endpoint and the PromptPay webhook): marking a
/// payment paid drives the lifecycle side-effects. Logic-only coverage on InMemory; rowversion and
/// temporal behaviour are covered by the SQL Server integration tests.
/// </summary>
public class PaymentSettlementTests
{
    private static readonly DateTime Now = new(2026, 6, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Inbound_premium_paid_activates_an_issued_policy()
    {
        await using var db = InMemoryAppDb.New();
        db.Policies.Add(new Policy
        {
            Id = 1, PolicyNo = "POL-2026-000001", CustomerId = 1, VehicleId = 1,
            Status = PolicyStatus.Issued, CoverageType = CoverageType.Type1, Premium = 12_000m,
        });
        db.Payments.Add(new Payment
        {
            Id = 10, PaymentNo = "PAY-2026-000010", Direction = PaymentDirection.Inbound,
            Status = PaymentStatus.Pending, PolicyId = 1, Amount = 12_000m,
        });
        await db.SaveChangesAsync();

        var settled = await PaymentSettlement.SettleAsync(db, new FixedClockProvider(Now), 10, "TXN-1", default);

        Assert.Equal(PaymentStatus.Paid, settled.Status);
        Assert.Equal(Now, settled.PaidAt);
        Assert.Equal("TXN-1", settled.ReferenceNo);
        Assert.Equal(PolicyStatus.Active, (await db.Policies.FindAsync(1L))!.Status);
    }

    [Fact]
    public async Task Outbound_payout_settled_moves_claim_approved_to_paid()
    {
        await using var db = InMemoryAppDb.New();
        db.Claims.Add(new Claim
        {
            Id = 5, ClaimNo = "CLM-2026-000005", PolicyId = 1, Status = ClaimStatus.Approved,
            IncidentDate = new DateOnly(2026, 6, 1), ClaimedAmount = 30_000m, ApprovedAmount = 25_000m,
        });
        db.Payments.Add(new Payment
        {
            Id = 20, PaymentNo = "PAY-2026-000020", Direction = PaymentDirection.Outbound,
            Status = PaymentStatus.Pending, ClaimId = 5, Amount = 25_000m,
        });
        await db.SaveChangesAsync();

        await PaymentSettlement.SettleAsync(db, new FixedClockProvider(Now), 20, "TXN-2", default);

        Assert.Equal(ClaimStatus.Paid, (await db.Claims.FindAsync(5L))!.Status);
    }

    [Fact]
    public async Task Settling_an_already_paid_payment_is_a_conflict()
    {
        await using var db = InMemoryAppDb.New();
        db.Payments.Add(new Payment
        {
            Id = 30, PaymentNo = "PAY-2026-000030", Direction = PaymentDirection.Inbound,
            Status = PaymentStatus.Paid, Amount = 1_000m,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            PaymentSettlement.SettleAsync(db, new FixedClockProvider(Now), 30, "TXN-3", default));
    }

    [Fact]
    public async Task Settling_premium_for_a_cancelled_policy_is_a_conflict()
    {
        await using var db = InMemoryAppDb.New();
        db.Policies.Add(new Policy
        {
            Id = 2, PolicyNo = "POL-2026-000002", CustomerId = 1, VehicleId = 1,
            Status = PolicyStatus.Cancelled, CoverageType = CoverageType.Type1, Premium = 9_000m,
        });
        db.Payments.Add(new Payment
        {
            Id = 40, PaymentNo = "PAY-2026-000040", Direction = PaymentDirection.Inbound,
            Status = PaymentStatus.Pending, PolicyId = 2, Amount = 9_000m,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            PaymentSettlement.SettleAsync(db, new FixedClockProvider(Now), 40, "TXN-4", default));
    }

    [Fact]
    public async Task Final_installment_paid_completes_the_plan_and_reactivates_a_suspended_policy()
    {
        await using var db = InMemoryAppDb.New();
        db.Policies.Add(new Policy
        {
            Id = 3, PolicyNo = "POL-2026-000003", CustomerId = 1, VehicleId = 1,
            Status = PolicyStatus.Suspended, CoverageType = CoverageType.Type1, Premium = 12_000m,
        });
        db.InstallmentPlans.Add(new InstallmentPlan
        {
            Id = 1, PolicyId = 3, TotalPremium = 12_000m, Fee = 0m, Installments = 2,
            FrequencyDays = 30, Status = InstallmentPlanStatus.Defaulted,
        });
        // First installment already paid; the overdue second one is being settled now.
        db.Payments.Add(new Payment
        {
            Id = 50, PaymentNo = "PAY-2026-000050", Direction = PaymentDirection.Inbound,
            Status = PaymentStatus.Paid, PolicyId = 3, InstallmentPlanId = 1, InstallmentSeq = 1, Amount = 6_000m,
        });
        db.Payments.Add(new Payment
        {
            Id = 51, PaymentNo = "PAY-2026-000051", Direction = PaymentDirection.Inbound,
            Status = PaymentStatus.Pending, PolicyId = 3, InstallmentPlanId = 1, InstallmentSeq = 2, Amount = 6_000m,
        });
        await db.SaveChangesAsync();

        await PaymentSettlement.SettleAsync(db, new FixedClockProvider(Now), 51, "TXN-5", default);

        Assert.Equal(PolicyStatus.Active, (await db.Policies.FindAsync(3L))!.Status);
        Assert.Equal(InstallmentPlanStatus.Completed, (await db.InstallmentPlans.FindAsync(1L))!.Status);
    }
}

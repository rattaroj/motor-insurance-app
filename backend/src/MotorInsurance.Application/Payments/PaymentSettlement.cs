using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;

namespace MotorInsurance.Application.Payments;

/// <summary>
/// The single hub for marking a payment paid and applying its lifecycle side-effects, shared by the
/// manual settle endpoint and the PromptPay webhook (auto-settle). Side effects by direction:
/// an inbound premium paid activates an Issued policy (or reactivates a Suspended one) and advances
/// its installment plan; an outbound payout settled moves the claim Approved → Paid. All status
/// changes go through the state machines, and the write carries the row's <c>rowversion</c> so two
/// concurrent settlements collide (DbUpdateConcurrencyException) rather than double-pay.
/// </summary>
public static class PaymentSettlement
{
    /// <summary>
    /// Loads the pending payment, applies the side-effects and saves. Throws
    /// <see cref="NotFoundException"/> when the payment is missing and <see cref="ConflictException"/>
    /// when it is not pending (or the policy can't take a premium).
    /// </summary>
    public static async Task<Payment> SettleAsync(
        IAppDbContext db, IDateTimeProvider clock, long paymentId, string referenceNo, CancellationToken ct)
    {
        var payment = await db.Payments
            .Include(p => p.Policy)
            .Include(p => p.Claim)
            .Include(p => p.InstallmentPlan).ThenInclude(ip => ip!.Payments)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct)
            ?? throw new NotFoundException(nameof(Payment), paymentId);

        if (payment.Status != PaymentStatus.Pending)
            throw new ConflictException($"Payment is already {payment.Status}.");

        Apply(payment, clock.UtcNow, referenceNo);
        await db.SaveChangesAsync(ct);
        return payment;
    }

    /// <summary>
    /// Applies the settlement to an already-loaded payment graph (status + side-effects), without
    /// saving. Exposed so callers that have already fetched the payment (e.g. the webhook, which
    /// validates the amount first) don't load it twice. The graph must include Policy/Claim/plan.
    /// </summary>
    public static void Apply(Payment payment, DateTime now, string referenceNo)
    {
        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = now;
        payment.ReferenceNo = referenceNo;

        if (payment is { Direction: PaymentDirection.Inbound, Policy: not null })
        {
            if (payment.Policy.Status is PolicyStatus.Cancelled or PolicyStatus.Expired)
                throw new ConflictException($"Cannot settle premium for a {payment.Policy.Status} policy.");

            // Premium paid → activate an Issued policy (first / down payment);
            // a later installment paid on a Suspended policy reactivates it.
            if (payment.Policy.Status is PolicyStatus.Issued or PolicyStatus.Suspended)
            {
                PolicyStateMachine.EnsureTransition(payment.Policy.Status, PolicyStatus.Active);
                payment.Policy.Status = PolicyStatus.Active;
            }

            // Installment plan: completed when every installment is settled;
            // resumed (Defaulted → Active) when a caught-up payment isn't the last.
            if (payment.InstallmentPlan is { } plan)
            {
                if (plan.Payments.All(x => x.Status == PaymentStatus.Paid))
                    plan.Status = InstallmentPlanStatus.Completed;
                else if (plan.Status == InstallmentPlanStatus.Defaulted)
                    plan.Status = InstallmentPlanStatus.Active;
            }
        }
        else if (payment is { Direction: PaymentDirection.Outbound, Claim: not null })
        {
            // Payout settled → claim moves Approved → Paid.
            ClaimStateMachine.EnsureTransition(payment.Claim.Status, ClaimStatus.Paid);
            payment.Claim.Status = ClaimStatus.Paid;
        }
    }
}

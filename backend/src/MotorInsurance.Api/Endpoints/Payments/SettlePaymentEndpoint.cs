using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Payments;

public record SettlePaymentRequest(string ReferenceNo);

public class SettlePaymentValidator : Validator<SettlePaymentRequest>
{
    public SettlePaymentValidator() => RuleFor(x => x.ReferenceNo).NotEmpty().MaximumLength(100);
}

/// <summary>
/// POST /api/payments/{id}/settle — mark a payment paid. Side effects: inbound premium paid
/// auto-activates an Issued policy; an outbound payout moves its claim Approved -> Paid.
/// </summary>
public class SettlePaymentEndpoint : Endpoint<SettlePaymentRequest>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public SettlePaymentEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Post("payments/{id}/settle");
        Policies(PermissionPolicy.For(Perms.PaymentSettle));
    }

    public override async Task HandleAsync(SettlePaymentRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var payment = await _db.Payments
            .Include(p => p.Policy)
            .Include(p => p.Claim)
            .Include(p => p.InstallmentPlan).ThenInclude(ip => ip!.Payments)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Payment), id);

        if (payment.Status != PaymentStatus.Pending)
            throw new ConflictException($"Payment is already {payment.Status}.");

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = _clock.UtcNow;
        payment.ReferenceNo = r.ReferenceNo;

        // Side effects by direction:
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

        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

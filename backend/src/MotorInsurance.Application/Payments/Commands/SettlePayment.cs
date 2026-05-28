using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;

namespace MotorInsurance.Application.Payments.Commands;

// ============================================================
// Settle a payment (mark paid). For inbound premium this can
// auto-activate the policy; for outbound it closes the claim flow.
// ============================================================
public record SettlePaymentCommand(long PaymentId, string ReferenceNo) : IRequest;

public class SettlePaymentValidator : AbstractValidator<SettlePaymentCommand>
{
    public SettlePaymentValidator() => RuleFor(x => x.ReferenceNo).NotEmpty().MaximumLength(100);
}

public class SettlePaymentHandler : IRequestHandler<SettlePaymentCommand>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SettlePaymentHandler(IAppDbContext db, IDateTimeProvider clock)
        => (_db, _clock) = (db, clock);

    public async Task Handle(SettlePaymentCommand req, CancellationToken ct)
    {
        var payment = await _db.Payments
            .Include(p => p.Policy)
            .Include(p => p.Claim)
            .FirstOrDefaultAsync(p => p.Id == req.PaymentId, ct)
            ?? throw new NotFoundException(nameof(Payment), req.PaymentId);

        if (payment.Status != PaymentStatus.Pending)
            throw new ConflictException($"Payment is already {payment.Status}.");

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = _clock.UtcNow;
        payment.ReferenceNo = req.ReferenceNo;

        // Side effects by direction:
        if (payment is { Direction: PaymentDirection.Inbound, Policy: not null })
        {
            if (payment.Policy.Status is PolicyStatus.Cancelled or PolicyStatus.Expired)
                throw new ConflictException($"Cannot settle premium for a {payment.Policy.Status} policy.");

            // Premium paid → activate policy if it is Issued.
            if (payment.Policy.Status == PolicyStatus.Issued)
            {
                PolicyStateMachine.EnsureTransition(payment.Policy.Status, PolicyStatus.Active);
                payment.Policy.Status = PolicyStatus.Active;
            }
        }
        else if (payment is { Direction: PaymentDirection.Outbound, Claim: not null })
        {
            // Payout settled → claim moves Approved → Paid.
            ClaimStateMachine.EnsureTransition(payment.Claim.Status, ClaimStatus.Paid);
            payment.Claim.Status = ClaimStatus.Paid;
        }

        await _db.SaveChangesAsync(ct);
    }
}

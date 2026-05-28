using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;

namespace MotorInsurance.Application.Policies.Commands;

// ============================================================
// Issue policy from an accepted quotation
// ============================================================
public record IssuePolicyCommand(long QuotationId, DateOnly EffectiveDate) : IRequest<long>;

public class IssuePolicyValidator : AbstractValidator<IssuePolicyCommand>
{
    public IssuePolicyValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.QuotationId).GreaterThan(0);
        RuleFor(x => x.EffectiveDate)
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(clock.UtcNow.Date))
            .WithMessage("Effective date cannot be in the past.");
    }
}

public class IssuePolicyHandler : IRequestHandler<IssuePolicyCommand, long>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public IssuePolicyHandler(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public async Task<long> Handle(IssuePolicyCommand req, CancellationToken ct)
    {
        var quote = await _db.Quotations.FirstOrDefaultAsync(q => q.Id == req.QuotationId, ct)
            ?? throw new NotFoundException(nameof(Quotation), req.QuotationId);

        if (quote.ValidUntil < DateOnly.FromDateTime(_clock.UtcNow.Date))
            throw new ConflictException("Quotation has expired and cannot be issued.");

        if (await _db.Policies.AnyAsync(p => p.QuotationId == quote.Id, ct))
            throw new ConflictException("A policy has already been issued from this quotation.");

        // A policy starts at Issued (premium captured, awaiting activation on payment).
        var policy = new Policy
        {
            PolicyNo = await _docNo.NextAsync("POL", ct),
            QuotationId = quote.Id,
            CustomerId = quote.CustomerId,
            VehicleId = quote.VehicleId,
            Status = PolicyStatus.Issued,
            CoverageType = quote.CoverageType,
            SumInsured = quote.SumInsured,
            Premium = quote.Premium,
            EffectiveDate = req.EffectiveDate,
            ExpiryDate = req.EffectiveDate.AddYears(1),
            CreatedAt = _clock.UtcNow,
        };

        _db.Policies.Add(policy);

        // Premium payment record (inbound, pending until paid).
        _db.Payments.Add(new Payment
        {
            PaymentNo = await _docNo.NextAsync("PAY", ct),
            Direction = PaymentDirection.Inbound,
            Status = PaymentStatus.Pending,
            Policy = policy,
            Amount = policy.Premium,
            CreatedAt = _clock.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return policy.Id;
    }
}

// ============================================================
// Activate policy (after premium paid)
// ============================================================
public record ActivatePolicyCommand(long PolicyId) : IRequest;

public class ActivatePolicyHandler : IRequestHandler<ActivatePolicyCommand>
{
    private readonly IAppDbContext _db;
    public ActivatePolicyHandler(IAppDbContext db) => _db = db;

    public async Task Handle(ActivatePolicyCommand req, CancellationToken ct)
    {
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.Id == req.PolicyId, ct)
            ?? throw new NotFoundException(nameof(Policy), req.PolicyId);

        var premiumPaid = await _db.Payments.AnyAsync(
            p => p.PolicyId == policy.Id
                 && p.Direction == PaymentDirection.Inbound
                 && p.Status == PaymentStatus.Paid, ct);
        if (!premiumPaid)
            throw new ConflictException("Cannot activate: premium has not been paid.");

        PolicyStateMachine.EnsureTransition(policy.Status, PolicyStatus.Active);
        policy.Status = PolicyStatus.Active;
        await _db.SaveChangesAsync(ct);
    }
}

// ============================================================
// Cancel policy
// ============================================================
public record CancelPolicyCommand(long PolicyId, string Reason) : IRequest;

public class CancelPolicyHandler : IRequestHandler<CancelPolicyCommand>
{
    private readonly IAppDbContext _db;
    public CancelPolicyHandler(IAppDbContext db) => _db = db;

    public async Task Handle(CancelPolicyCommand req, CancellationToken ct)
    {
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.Id == req.PolicyId, ct)
            ?? throw new NotFoundException(nameof(Policy), req.PolicyId);

        PolicyStateMachine.EnsureTransition(policy.Status, PolicyStatus.Cancelled);
        policy.Status = PolicyStatus.Cancelled;
        await _db.SaveChangesAsync(ct);
    }
}

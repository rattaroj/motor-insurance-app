using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Quotations.Commands;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Renewals.Commands;

// ============================================================
// Policy renewal flow
// ------------------------------------------------------------
// A renewal creates a NEW policy linked to the previous one via
// PreviousPolicyId. The new policy starts at Issued with a fresh
// premium (optionally adjusted), effective the day after the old
// one expires. The old policy is left to expire naturally.
//
// Renewal eligibility:
//   - previous policy must be Active
//   - within the renewal window (e.g. 60 days before expiry)
// ============================================================
public record RenewPolicyCommand(
    long PolicyId,
    decimal? AdjustedSumInsured = null) : IRequest<long>;

public class RenewPolicyValidator : AbstractValidator<RenewPolicyCommand>
{
    public RenewPolicyValidator()
    {
        RuleFor(x => x.PolicyId).GreaterThan(0);
        RuleFor(x => x.AdjustedSumInsured)
            .GreaterThan(0).When(x => x.AdjustedSumInsured.HasValue);
    }
}

public class RenewPolicyHandler : IRequestHandler<RenewPolicyCommand, long>
{
    private const int RenewalWindowDays = 60;

    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public RenewPolicyHandler(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public async Task<long> Handle(RenewPolicyCommand req, CancellationToken ct)
    {
        var prev = await _db.Policies.FirstOrDefaultAsync(p => p.Id == req.PolicyId, ct)
            ?? throw new NotFoundException(nameof(Policy), req.PolicyId);

        if (prev.Status != PolicyStatus.Active)
            throw new ConflictException("Only an active policy can be renewed.");

        if (prev.ExpiryDate is null)
            throw new ConflictException("Policy has no expiry date.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var windowOpens = prev.ExpiryDate.Value.AddDays(-RenewalWindowDays);
        if (today < windowOpens)
            throw new ConflictException(
                $"Renewal window opens on {windowOpens:yyyy-MM-dd} ({RenewalWindowDays} days before expiry).");

        // Guard against duplicate renewal.
        var alreadyRenewed = await _db.Policies.AnyAsync(p => p.PreviousPolicyId == prev.Id, ct);
        if (alreadyRenewed)
            throw new ConflictException("This policy has already been renewed.");

        var sumInsured = req.AdjustedSumInsured ?? prev.SumInsured;
        var newEffective = prev.ExpiryDate.Value.AddDays(1);

        var renewal = new Policy
        {
            PolicyNo = await _docNo.NextAsync("POL", ct),
            QuotationId = null,
            CustomerId = prev.CustomerId,
            VehicleId = prev.VehicleId,
            Status = PolicyStatus.Issued,
            CoverageType = prev.CoverageType,
            SumInsured = sumInsured,
            Premium = PremiumCalculator.Calculate(prev.CoverageType, sumInsured),
            EffectiveDate = newEffective,
            ExpiryDate = newEffective.AddYears(1),
            PreviousPolicyId = prev.Id,
            CreatedAt = _clock.UtcNow,
        };
        _db.Policies.Add(renewal);

        _db.Payments.Add(new Payment
        {
            PaymentNo = await _docNo.NextAsync("PAY", ct),
            Direction = PaymentDirection.Inbound,
            Status = PaymentStatus.Pending,
            Policy = renewal,
            Amount = renewal.Premium,
            CreatedAt = _clock.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        return renewal.Id;
    }
}

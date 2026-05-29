using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Renewals;

public record RenewPolicyRequest(decimal? AdjustedSumInsured);
public record RenewPolicyResponse(long Id);

public class RenewPolicyValidator : Validator<RenewPolicyRequest>
{
    public RenewPolicyValidator() =>
        RuleFor(x => x.AdjustedSumInsured).GreaterThan(0).When(x => x.AdjustedSumInsured.HasValue);
}

/// <summary>
/// POST /api/renewals/{policyId} — create a NEW policy linked to the previous one
/// (PreviousPolicyId), starting at Issued with a fresh premium, effective the day after
/// the old policy expires. Requires the previous policy Active and within the 60-day window.
/// </summary>
public class RenewPolicyEndpoint : Endpoint<RenewPolicyRequest, RenewPolicyResponse>
{
    private const int RenewalWindowDays = 60;

    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public RenewPolicyEndpoint(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public override void Configure()
    {
        Post("renewals/{policyId}");
        Policies(PermissionPolicy.For(Perms.PolicyRenew));
    }

    public override async Task HandleAsync(RenewPolicyRequest r, CancellationToken ct)
    {
        var policyId = Route<long>("policyId");

        var prev = await _db.Policies.FirstOrDefaultAsync(p => p.Id == policyId, ct)
            ?? throw new NotFoundException(nameof(Policy), policyId);

        if (prev.Status != PolicyStatus.Active)
            throw new ConflictException("Only an active policy can be renewed.");

        if (prev.ExpiryDate is null)
            throw new ConflictException("Policy has no expiry date.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var windowOpens = prev.ExpiryDate.Value.AddDays(-RenewalWindowDays);
        if (today < windowOpens)
            throw new ConflictException(
                $"Renewal window opens on {windowOpens:yyyy-MM-dd} ({RenewalWindowDays} days before expiry).");

        if (await _db.Policies.AnyAsync(p => p.PreviousPolicyId == prev.Id, ct))
            throw new ConflictException("This policy has already been renewed.");

        var sumInsured = r.AdjustedSumInsured ?? prev.SumInsured;
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
        await Send.ResponseAsync(new RenewPolicyResponse(renewal.Id), 201, ct);
    }
}

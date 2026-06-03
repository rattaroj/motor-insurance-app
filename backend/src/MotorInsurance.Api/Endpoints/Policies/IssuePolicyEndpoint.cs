using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

public record IssuePolicyRequest(long QuotationId, DateOnly EffectiveDate);
public record IssuePolicyResponse(long Id);

public class IssuePolicyValidator : Validator<IssuePolicyRequest>
{
    public IssuePolicyValidator(IDateTimeProvider clock)
    {
        RuleFor(x => x.QuotationId).GreaterThan(0);
        RuleFor(x => x.EffectiveDate)
            .GreaterThanOrEqualTo(_ => DateOnly.FromDateTime(clock.UtcNow.Date))
            .WithMessage("Effective date cannot be in the past.");
    }
}

/// <summary>POST /api/policies/issue — issue a policy from an accepted quotation.</summary>
public class IssuePolicyEndpoint : Endpoint<IssuePolicyRequest, IssuePolicyResponse>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public IssuePolicyEndpoint(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public override void Configure()
    {
        Post("policies/issue");
        Policies(PermissionPolicy.For(Perms.PolicyIssue));
    }

    public override async Task HandleAsync(IssuePolicyRequest r, CancellationToken ct)
    {
        var policyId = await IssueAsync(r, ct);
        await Send.ResponseAsync(new IssuePolicyResponse(policyId), 201, ct);
    }

    /// <summary>Core logic, separated so it is unit-testable without the HTTP layer.</summary>
    public async Task<long> IssueAsync(IssuePolicyRequest r, CancellationToken ct)
    {
        var quote = await _db.Quotations
            .Include(q => q.Riders)
            .FirstOrDefaultAsync(q => q.Id == r.QuotationId, ct)
            ?? throw new NotFoundException(nameof(Quotation), r.QuotationId);

        if (quote.ValidUntil < DateOnly.FromDateTime(_clock.UtcNow.Date))
            throw new ConflictException("Quotation has expired and cannot be issued.");

        if (await _db.Policies.AnyAsync(p => p.QuotationId == quote.Id, ct))
            throw new ConflictException("A policy has already been issued from this quotation.");

        // A policy starts at Issued (premium captured, awaiting activation on payment).
        // The full rating (base/NCB/deductible + riders) is carried forward from the quotation.
        var policy = new Policy
        {
            PolicyNo = await _docNo.NextAsync("POL", ct),
            QuotationId = quote.Id,
            CustomerId = quote.CustomerId,
            VehicleId = quote.VehicleId,
            Status = PolicyStatus.Issued,
            CoverageType = quote.CoverageType,
            SumInsured = quote.SumInsured,
            BasePremium = quote.BasePremium,
            Premium = quote.Premium,
            NcbPercent = quote.NcbPercent,
            Deductible = quote.Deductible,
            EffectiveDate = r.EffectiveDate,
            ExpiryDate = r.EffectiveDate.AddYears(1),
            CreatedAt = _clock.UtcNow,
            Riders = quote.Riders.Select(qr => new PolicyRider { RiderId = qr.RiderId }).ToList(),
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

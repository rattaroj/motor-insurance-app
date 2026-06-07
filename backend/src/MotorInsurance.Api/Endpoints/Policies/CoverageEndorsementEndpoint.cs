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

namespace MotorInsurance.Api.Endpoints.Policies;

public record CoverageEndorsementRequest(
    CoverageType? NewCoverageType, decimal? NewSumInsured, IReadOnlyList<long>? NewRiderIds,
    DateOnly EffectiveDate, string? Note);

public record CoverageEndorsementResponse(
    IReadOnlyList<string> EndorsementNos, decimal NewPremium, decimal PremiumDelta, string? PaymentNo);

public class CoverageEndorsementValidator : Validator<CoverageEndorsementRequest>
{
    public CoverageEndorsementValidator()
    {
        RuleFor(x => x.NewSumInsured).GreaterThan(0).LessThanOrEqualTo(50_000_000)
            .When(x => x.NewSumInsured is not null);
        RuleFor(x => x)
            .Must(x => x.NewCoverageType is not null || x.NewSumInsured is not null || x.NewRiderIds is not null)
            .WithMessage("ต้องระบุการเปลี่ยนแปลงอย่างน้อยหนึ่งอย่าง (ชั้น/ทุนประกัน/ความคุ้มครองเสริม)");
    }
}

/// <summary>
/// POST /api/policies/{id}/coverage-endorsement — mid-term endorsement that changes the coverage
/// class, sum insured and/or riders, re-rates the premium, and raises the pro-rata additional
/// (inbound) or return (outbound) premium as a pending payment. Active policies only.
/// </summary>
public class CoverageEndorsementEndpoint : Endpoint<CoverageEndorsementRequest, CoverageEndorsementResponse>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;
    public CoverageEndorsementEndpoint(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public override void Configure()
    {
        Post("policies/{id}/coverage-endorsement");
        Policies(PermissionPolicy.For(Perms.PolicyEndorse));
    }

    public override async Task HandleAsync(CoverageEndorsementRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var response = await EndorseCoverageAsync(id, r, ct);
        await Send.ResponseAsync(response, 201, ct);
    }

    /// <summary>Core logic, separated so it is unit-testable without the HTTP layer.</summary>
    public async Task<CoverageEndorsementResponse> EndorseCoverageAsync(
        long id, CoverageEndorsementRequest r, CancellationToken ct)
    {
        var policy = await _db.Policies.Include(p => p.Riders)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Policy), id);

        if (policy.Status != PolicyStatus.Active)
            throw new ConflictException("สลักหลังเปลี่ยนความคุ้มครองทำได้เฉพาะกรมธรรม์ที่คุ้มครองอยู่");

        var newCoverage = r.NewCoverageType ?? policy.CoverageType;
        var newSum = r.NewSumInsured ?? policy.SumInsured;
        var currentRiderIds = policy.Riders.Select(x => x.RiderId).OrderBy(x => x).ToList();
        var newRiderIds = (r.NewRiderIds?.Distinct().OrderBy(x => x).ToList()) ?? currentRiderIds;

        var changes = new List<(string Field, string? Old, string? New)>();
        if (newCoverage != policy.CoverageType)
            changes.Add(("CoverageType", policy.CoverageType.ToString(), newCoverage.ToString()));
        if (newSum != policy.SumInsured)
            changes.Add(("SumInsured", policy.SumInsured.ToString("0.##"), newSum.ToString("0.##")));
        if (!currentRiderIds.SequenceEqual(newRiderIds))
            changes.Add(("Riders", string.Join(",", currentRiderIds), string.Join(",", newRiderIds)));

        if (changes.Count == 0)
            throw new ConflictException("ไม่พบการเปลี่ยนแปลง");

        // Re-rate with the policy's existing NCB/deductible but the new coverage/sum/riders.
        var rating = await PremiumRatingService.RateAsync(
            _db, _clock.UtcNow.Year, policy.VehicleId, newCoverage, newSum,
            policy.NcbPercent, policy.Deductible, newRiderIds, ct);
        var newNet = rating.Breakdown.NetPremium;
        var delta = Math.Round((newNet - policy.Premium) * ProRataFactor(policy, r.EffectiveDate), 2);

        // Record one endorsement row per changed field.
        var nos = new List<string>();
        foreach (var (field, oldValue, newValue) in changes)
        {
            var no = await _docNo.NextAsync("END", ct);
            nos.Add(no);
            _db.Endorsements.Add(new Endorsement
            {
                EndorsementNo = no,
                PolicyId = policy.Id,
                FieldName = field,
                OldValue = oldValue,
                NewValue = newValue,
                EffectiveDate = r.EffectiveDate,
                Note = r.Note,
                CreatedAt = _clock.UtcNow,
            });
        }

        // Apply the new coverage to the policy.
        policy.CoverageType = newCoverage;
        policy.SumInsured = newSum;
        policy.BasePremium = rating.Breakdown.BasePremium;
        policy.Premium = newNet;
        if (!currentRiderIds.SequenceEqual(newRiderIds))
        {
            _db.PolicyRiders.RemoveRange(policy.Riders);
            foreach (var rid in newRiderIds)
                _db.PolicyRiders.Add(new PolicyRider { PolicyId = policy.Id, RiderId = rid });
        }

        // Pro-rata additional (inbound) or return (outbound) premium as a pending payment.
        string? paymentNo = null;
        if (delta != 0)
        {
            paymentNo = await _docNo.NextAsync("PAY", ct);
            _db.Payments.Add(new Payment
            {
                PaymentNo = paymentNo,
                Direction = delta > 0 ? PaymentDirection.Inbound : PaymentDirection.Outbound,
                Status = PaymentStatus.Pending,
                PolicyId = policy.Id,
                Amount = Math.Abs(delta),
                CreatedAt = _clock.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
        return new CoverageEndorsementResponse(nos, newNet, delta, paymentNo);
    }

    /// <summary>Fraction of the policy term still unexpired from the endorsement's effective date.</summary>
    private decimal ProRataFactor(Policy p, DateOnly effectiveDate)
    {
        if (p.EffectiveDate is null || p.ExpiryDate is null) return 1m;
        var totalDays = p.ExpiryDate.Value.DayNumber - p.EffectiveDate.Value.DayNumber;
        if (totalDays <= 0) return 1m;

        var from = effectiveDate < p.EffectiveDate.Value ? p.EffectiveDate.Value : effectiveDate;
        var remaining = Math.Clamp(p.ExpiryDate.Value.DayNumber - from.DayNumber, 0, totalDays);
        return (decimal)remaining / totalDays;
    }
}

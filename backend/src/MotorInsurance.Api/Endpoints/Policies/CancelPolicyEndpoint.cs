using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

public record CancelPolicyRequest(string Reason);
public record CancelPolicyResponse(decimal RefundAmount, string? RefundPaymentNo);

/// <summary>
/// POST /api/policies/{id}/cancel — cancel a policy. If it was Active (premium paid), a pro-rata
/// refund for the unexpired period is raised as a pending outbound payment for finance to settle.
/// </summary>
public class CancelPolicyEndpoint : Endpoint<CancelPolicyRequest, CancelPolicyResponse>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;
    public CancelPolicyEndpoint(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public override void Configure()
    {
        Post("policies/{id}/cancel");
        Policies(PermissionPolicy.For(Perms.PolicyCancel));
    }

    public override async Task HandleAsync(CancelPolicyRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Policy), id);

        var wasActive = policy.Status == PolicyStatus.Active;
        PolicyStateMachine.EnsureTransition(policy.Status, PolicyStatus.Cancelled);
        policy.Status = PolicyStatus.Cancelled;

        // Pro-rata refund only when the premium was actually paid (policy was Active).
        var refund = wasActive ? ProRataRefund(policy) : 0m;
        string? refundNo = null;
        if (refund > 0)
        {
            refundNo = await _docNo.NextAsync("PAY", ct);
            _db.Payments.Add(new Payment
            {
                PaymentNo = refundNo,
                Direction = PaymentDirection.Outbound,
                Status = PaymentStatus.Pending,
                PolicyId = policy.Id,
                Amount = refund,
                CreatedAt = _clock.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
        await Send.ResponseAsync(new CancelPolicyResponse(refund, refundNo), 200, ct);
    }

    /// <summary>Refund = premium × (remaining days / total days), clamped, rounded to 2dp.</summary>
    private decimal ProRataRefund(Policy p)
    {
        if (p.EffectiveDate is null || p.ExpiryDate is null) return 0m;
        var totalDays = p.ExpiryDate.Value.DayNumber - p.EffectiveDate.Value.DayNumber;
        if (totalDays <= 0) return 0m;

        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var remaining = Math.Clamp(p.ExpiryDate.Value.DayNumber - today.DayNumber, 0, totalDays);
        return Math.Round(p.Premium * remaining / totalDays, 2);
    }
}

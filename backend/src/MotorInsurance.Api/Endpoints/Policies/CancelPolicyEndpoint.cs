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

        PolicyStateMachine.EnsureTransition(policy.Status, PolicyStatus.Cancelled);
        policy.Status = PolicyStatus.Cancelled;

        // Refund the unearned part of what was actually paid (pro-rata by elapsed coverage). For a
        // full single payment this equals premium × remaining/total; for installments it only refunds
        // what the customer actually paid in, never the unpaid future installments.
        var amountPaid = await _db.Payments.AsNoTracking()
            .Where(p => p.PolicyId == id && p.Direction == PaymentDirection.Inbound && p.Status == PaymentStatus.Paid)
            .Select(p => (decimal?)p.Amount).SumAsync(ct) ?? 0m;

        // Void any still-pending installments so they're no longer collectible.
        var pending = await _db.Payments
            .Where(p => p.PolicyId == id && p.Direction == PaymentDirection.Inbound
                && p.Status == PaymentStatus.Pending && p.InstallmentPlanId != null)
            .ToListAsync(ct);
        foreach (var pmt in pending) pmt.Status = PaymentStatus.Failed;

        var refund = ProRataRefund(policy, amountPaid);
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

    /// <summary>
    /// Refund = amountPaid − earned premium, where earned = premium × (elapsed days / total days),
    /// clamped at 0 and rounded to 2dp. For a fully-paid policy this equals premium × remaining/total.
    /// </summary>
    private decimal ProRataRefund(Policy p, decimal amountPaid)
    {
        if (amountPaid <= 0 || p.EffectiveDate is null || p.ExpiryDate is null) return 0m;
        var totalDays = p.ExpiryDate.Value.DayNumber - p.EffectiveDate.Value.DayNumber;
        if (totalDays <= 0) return 0m;

        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var elapsed = Math.Clamp(today.DayNumber - p.EffectiveDate.Value.DayNumber, 0, totalDays);
        var earned = p.Premium * elapsed / totalDays;
        return Math.Max(0m, Math.Round(amountPaid - earned, 2));
    }
}

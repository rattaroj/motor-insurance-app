using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Notifications;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public record ApproveClaimRequest(decimal ApprovedAmount);

public class ApproveClaimValidator : Validator<ApproveClaimRequest>
{
    public ApproveClaimValidator() => RuleFor(x => x.ApprovedAmount).GreaterThan(0);
}

/// <summary>POST /api/claims/{id}/approve — approve a claim and create the outbound payout (pending).</summary>
public class ApproveClaimEndpoint : Endpoint<ApproveClaimRequest>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public ApproveClaimEndpoint(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public override void Configure()
    {
        Post("claims/{id}/approve");
        Policies(PermissionPolicy.For(Perms.ClaimApprove));
    }

    public override async Task HandleAsync(ApproveClaimRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(Claim), id);

        if (r.ApprovedAmount > claim.ClaimedAmount)
            throw new ConflictException("Approved amount cannot exceed the claimed amount.");

        ClaimStateMachine.EnsureTransition(claim.Status, ClaimStatus.Approved);
        claim.Status = ClaimStatus.Approved;
        claim.ApprovedAmount = r.ApprovedAmount;

        _db.Payments.Add(new Payment
        {
            PaymentNo = await _docNo.NextAsync("PAY", ct),
            Direction = PaymentDirection.Outbound,
            Status = PaymentStatus.Pending,
            ClaimId = claim.Id,
            Amount = r.ApprovedAmount,
            CreatedAt = _clock.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
        await NotifyApprovedAsync(claim, ct);
        await Send.NoContentAsync(ct);
    }

    /// <summary>Best-effort "claim approved" notification to the policyholder.</summary>
    private async Task NotifyApprovedAsync(Claim claim, CancellationToken ct)
    {
        var sender = TryResolve<INotificationSender>();
        if (sender is null) return;
        try
        {
            await NotificationDispatcher.SendToPolicyCustomerAsync(
                _db, sender, _clock, claim.PolicyId,
                $"อนุมัติเคลม {claim.ClaimNo}",
                $"เคลมเลขที่ {claim.ClaimNo} ได้รับอนุมัติเป็นจำนวนเงิน {claim.ApprovedAmount:N2} บาท", ct);
        }
        catch
        {
            // Best-effort: never block claim approval on a notification failure.
        }
    }
}

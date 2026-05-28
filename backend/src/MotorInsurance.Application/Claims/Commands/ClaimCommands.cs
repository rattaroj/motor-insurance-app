using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;

namespace MotorInsurance.Application.Claims.Commands;

// ============================================================
// File a new claim against an active policy
// ============================================================
public record FileClaimCommand(
    long PolicyId,
    DateOnly IncidentDate,
    string? Description,
    decimal ClaimedAmount) : IRequest<long>;

public class FileClaimValidator : AbstractValidator<FileClaimCommand>
{
    public FileClaimValidator()
    {
        RuleFor(x => x.PolicyId).GreaterThan(0);
        RuleFor(x => x.ClaimedAmount).GreaterThan(0);
        RuleFor(x => x.IncidentDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("Incident date cannot be in the future.");
    }
}

public class FileClaimHandler : IRequestHandler<FileClaimCommand, long>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public FileClaimHandler(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public async Task<long> Handle(FileClaimCommand req, CancellationToken ct)
    {
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.Id == req.PolicyId, ct)
            ?? throw new NotFoundException(nameof(Policy), req.PolicyId);

        if (policy.Status != PolicyStatus.Active)
            throw new ConflictException("Claims can only be filed against an active policy.");

        if (req.ClaimedAmount > policy.SumInsured)
            throw new ConflictException("Claimed amount exceeds the policy sum insured.");

        var claim = new Claim
        {
            ClaimNo = await _docNo.NextAsync("CLM", ct),
            PolicyId = policy.Id,
            Status = ClaimStatus.Filed,
            IncidentDate = req.IncidentDate,
            Description = req.Description,
            ClaimedAmount = req.ClaimedAmount,
            CreatedAt = _clock.UtcNow,
        };
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync(ct);
        return claim.Id;
    }
}

// ============================================================
// Advance claim through review/assessment
// ============================================================
public record AdvanceClaimCommand(long ClaimId, ClaimStatus To) : IRequest;

public class AdvanceClaimHandler : IRequestHandler<AdvanceClaimCommand>
{
    private readonly IAppDbContext _db;
    public AdvanceClaimHandler(IAppDbContext db) => _db = db;

    public async Task Handle(AdvanceClaimCommand req, CancellationToken ct)
    {
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == req.ClaimId, ct)
            ?? throw new NotFoundException(nameof(Claim), req.ClaimId);

        ClaimStateMachine.EnsureTransition(claim.Status, req.To);
        claim.Status = req.To;
        await _db.SaveChangesAsync(ct);
    }
}

// ============================================================
// Approve claim (sets approved amount, creates outbound payment)
// ============================================================
public record ApproveClaimCommand(long ClaimId, decimal ApprovedAmount) : IRequest;

public class ApproveClaimValidator : AbstractValidator<ApproveClaimCommand>
{
    public ApproveClaimValidator() => RuleFor(x => x.ApprovedAmount).GreaterThan(0);
}

public class ApproveClaimHandler : IRequestHandler<ApproveClaimCommand>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public ApproveClaimHandler(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public async Task Handle(ApproveClaimCommand req, CancellationToken ct)
    {
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == req.ClaimId, ct)
            ?? throw new NotFoundException(nameof(Claim), req.ClaimId);

        if (req.ApprovedAmount > claim.ClaimedAmount)
            throw new ConflictException("Approved amount cannot exceed the claimed amount.");

        ClaimStateMachine.EnsureTransition(claim.Status, ClaimStatus.Approved);
        claim.Status = ClaimStatus.Approved;
        claim.ApprovedAmount = req.ApprovedAmount;

        _db.Payments.Add(new Payment
        {
            PaymentNo = await _docNo.NextAsync("PAY", ct),
            Direction = PaymentDirection.Outbound,
            Status = PaymentStatus.Pending,
            ClaimId = claim.Id,
            Amount = req.ApprovedAmount,
            CreatedAt = _clock.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
    }
}

// ============================================================
// Reject claim
// ============================================================
public record RejectClaimCommand(long ClaimId, string Reason) : IRequest;

public class RejectClaimValidator : AbstractValidator<RejectClaimCommand>
{
    public RejectClaimValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public class RejectClaimHandler : IRequestHandler<RejectClaimCommand>
{
    private readonly IAppDbContext _db;
    public RejectClaimHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RejectClaimCommand req, CancellationToken ct)
    {
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == req.ClaimId, ct)
            ?? throw new NotFoundException(nameof(Claim), req.ClaimId);

        ClaimStateMachine.EnsureTransition(claim.Status, ClaimStatus.Rejected);
        claim.Status = ClaimStatus.Rejected;
        claim.RejectReason = req.Reason;
        await _db.SaveChangesAsync(ct);
    }
}

using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public record FileClaimRequest(long PolicyId, DateOnly IncidentDate, string? Description, decimal ClaimedAmount);
public record FileClaimResponse(long Id);

public class FileClaimValidator : Validator<FileClaimRequest>
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

/// <summary>POST /api/claims — file a new claim against an active policy.</summary>
public class FileClaimEndpoint : Endpoint<FileClaimRequest, FileClaimResponse>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public FileClaimEndpoint(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public override void Configure()
    {
        Post("claims");
        Policies(PermissionPolicy.For(Perms.ClaimFile));
    }

    public override async Task HandleAsync(FileClaimRequest r, CancellationToken ct)
    {
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.Id == r.PolicyId, ct)
            ?? throw new NotFoundException(nameof(Policy), r.PolicyId);

        if (policy.Status != PolicyStatus.Active)
            throw new ConflictException("Claims can only be filed against an active policy.");

        if (r.ClaimedAmount > policy.SumInsured)
            throw new ConflictException("Claimed amount exceeds the policy sum insured.");

        var claim = new Claim
        {
            ClaimNo = await _docNo.NextAsync("CLM", ct),
            PolicyId = policy.Id,
            Status = ClaimStatus.Filed,
            IncidentDate = r.IncidentDate,
            Description = r.Description,
            ClaimedAmount = r.ClaimedAmount,
            CreatedAt = _clock.UtcNow,
        };
        _db.Claims.Add(claim);
        await _db.SaveChangesAsync(ct);

        await Send.ResponseAsync(new FileClaimResponse(claim.Id), 201, ct);
    }
}

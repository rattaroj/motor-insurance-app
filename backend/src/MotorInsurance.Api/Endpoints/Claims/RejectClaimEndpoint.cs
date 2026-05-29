using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public record RejectClaimRequest(string Reason);

public class RejectClaimValidator : Validator<RejectClaimRequest>
{
    public RejectClaimValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

/// <summary>POST /api/claims/{id}/reject.</summary>
public class RejectClaimEndpoint : Endpoint<RejectClaimRequest>
{
    private readonly IAppDbContext _db;
    public RejectClaimEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("claims/{id}/reject");
        Policies(PermissionPolicy.For(Perms.ClaimReject));
    }

    public override async Task HandleAsync(RejectClaimRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(Claim), id);

        ClaimStateMachine.EnsureTransition(claim.Status, ClaimStatus.Rejected);
        claim.Status = ClaimStatus.Rejected;
        claim.RejectReason = r.Reason;
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}

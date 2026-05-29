using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public record AdvanceClaimRequest(ClaimStatus To);

/// <summary>POST /api/claims/{id}/advance — move a claim through review/assessment.</summary>
public class AdvanceClaimEndpoint : Endpoint<AdvanceClaimRequest>
{
    private readonly IAppDbContext _db;
    public AdvanceClaimEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("claims/{id}/advance");
        Policies(PermissionPolicy.For(Perms.ClaimReview));
    }

    public override async Task HandleAsync(AdvanceClaimRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(Claim), id);

        ClaimStateMachine.EnsureTransition(claim.Status, r.To);
        claim.Status = r.To;
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}

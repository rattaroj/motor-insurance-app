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

/// <summary>POST /api/policies/{id}/cancel.</summary>
public class CancelPolicyEndpoint : Endpoint<CancelPolicyRequest>
{
    private readonly IAppDbContext _db;
    public CancelPolicyEndpoint(IAppDbContext db) => _db = db;

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
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}

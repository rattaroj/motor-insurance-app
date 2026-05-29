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

/// <summary>POST /api/policies/{id}/activate — activate an Issued policy once its premium is paid.</summary>
public class ActivatePolicyEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public ActivatePolicyEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("policies/{id}/activate");
        Policies(PermissionPolicy.For(Perms.PolicyActivate));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Policy), id);

        var premiumPaid = await _db.Payments.AnyAsync(
            p => p.PolicyId == policy.Id
                 && p.Direction == PaymentDirection.Inbound
                 && p.Status == PaymentStatus.Paid, ct);
        if (!premiumPaid)
            throw new ConflictException("Cannot activate: premium has not been paid.");

        PolicyStateMachine.EnsureTransition(policy.Status, PolicyStatus.Active);
        policy.Status = PolicyStatus.Active;
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}

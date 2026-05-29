using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

/// <summary>GET /api/policies/{id}.</summary>
public class GetPolicyEndpoint : EndpointWithoutRequest<PolicyDto>
{
    private readonly IAppDbContext _db;
    public GetPolicyEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("policies/{id}");
        Policies(PermissionPolicy.For(Perms.PolicyRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var policy = await _db.Policies
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Policy), id);

        Response = PolicyMapping.ToDto(policy);
    }
}

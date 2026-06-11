using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Application.Common.Interfaces;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public record BulkAssignClaimsRequest(IReadOnlyList<long> ClaimIds, long? GarageId, string? SurveyorName);
public record BulkAssignClaimsResponse(int Requested, int Assigned);

public class BulkAssignClaimsValidator : Validator<BulkAssignClaimsRequest>
{
    public BulkAssignClaimsValidator()
    {
        RuleFor(x => x.ClaimIds).NotEmpty();
        RuleFor(x => x.SurveyorName).MaximumLength(150);
    }
}

/// <summary>
/// POST /api/claims/assign-bulk — assign the same repair shop and/or surveyor to several claims
/// at once (the claims worklist "assign selected" action). Mirrors the renewals bulk-remind shape.
/// </summary>
public class BulkAssignClaimsEndpoint : Endpoint<BulkAssignClaimsRequest, BulkAssignClaimsResponse>
{
    private readonly IAppDbContext _db;
    public BulkAssignClaimsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("claims/assign-bulk");
        Policies(PermissionPolicy.For(Perms.ClaimReview));
    }

    public override async Task HandleAsync(BulkAssignClaimsRequest r, CancellationToken ct)
    {
        if (r.GarageId is { } gid && !await _db.Garages.AnyAsync(g => g.Id == gid, ct))
            throw new NotFoundException(nameof(Garage), gid);

        var surveyor = string.IsNullOrWhiteSpace(r.SurveyorName) ? null : r.SurveyorName.Trim();
        var claims = await _db.Claims.Where(c => r.ClaimIds.Contains(c.Id)).ToListAsync(ct);

        foreach (var claim in claims)
        {
            claim.GarageId = r.GarageId;
            claim.SurveyorName = surveyor;
        }
        await _db.SaveChangesAsync(ct);

        Response = new BulkAssignClaimsResponse(r.ClaimIds.Count, claims.Count);
    }
}

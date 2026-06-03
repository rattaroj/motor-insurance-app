using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public record AssignClaimRequest(long? GarageId, string? SurveyorName);

public class AssignClaimValidator : Validator<AssignClaimRequest>
{
    public AssignClaimValidator() => RuleFor(x => x.SurveyorName).MaximumLength(150);
}

/// <summary>POST /api/claims/{id}/assign — assign a repair shop and/or a surveyor to a claim.</summary>
public class AssignClaimEndpoint : Endpoint<AssignClaimRequest>
{
    private readonly IAppDbContext _db;
    public AssignClaimEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("claims/{id}/assign");
        Policies(PermissionPolicy.For(Perms.ClaimReview));
    }

    public override async Task HandleAsync(AssignClaimRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(Claim), id);

        if (r.GarageId is { } gid && !await _db.Garages.AnyAsync(g => g.Id == gid, ct))
            throw new NotFoundException(nameof(Garage), gid);

        claim.GarageId = r.GarageId;
        claim.SurveyorName = string.IsNullOrWhiteSpace(r.SurveyorName) ? null : r.SurveyorName.Trim();
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

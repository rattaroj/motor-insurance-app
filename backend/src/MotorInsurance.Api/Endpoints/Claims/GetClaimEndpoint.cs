using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Claims;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public record ClaimPhotoDto(long Id, string ImagePath, DateTime CreatedAt);

public record ClaimDetailDto(
    long Id, string ClaimNo, long PolicyId, string PolicyNo, string Status,
    DateOnly IncidentDate, string? Description, decimal ClaimedAmount, decimal? ApprovedAmount, string? RejectReason,
    long? GarageId, string? GarageName, string? GaragePhone, string? SurveyorName,
    IReadOnlyList<ClaimPhotoDto> Photos,
    IReadOnlyList<ClaimRiskFlag> RiskFlags,
    AuditInfo Audit);

/// <summary>GET /api/claims/{id} — claim detail incl. garage, surveyor and damage photos.</summary>
public class GetClaimEndpoint : EndpointWithoutRequest<ClaimDetailDto>
{
    private readonly IAppDbContext _db;
    public GetClaimEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("claims/{id}");
        Policies(PermissionPolicy.For(Perms.ClaimRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var c = await _db.Claims.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.ClaimNo,
                x.PolicyId,
                PolicyNo = x.Policy.PolicyNo,
                x.Status,
                x.IncidentDate,
                x.Description,
                x.ClaimedAmount,
                x.ApprovedAmount,
                x.RejectReason,
                x.GarageId,
                GarageName = x.Garage != null ? x.Garage.Name : null,
                GaragePhone = x.Garage != null ? x.Garage.Phone : null,
                x.SurveyorName,
                PolicyEffectiveDate = x.Policy.EffectiveDate,
                PolicySumInsured = x.Policy.SumInsured,
                x.CreatedUser,
                x.CreatedAt,
                x.UpdatedUser,
                x.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Claim), id);

        var photos = await _db.ClaimPhotos.AsNoTracking()
            .Where(p => p.ClaimId == id).OrderBy(p => p.Id)
            .Select(p => new ClaimPhotoDto(p.Id, p.ImagePath, p.CreatedAt))
            .ToListAsync(ct);

        // Other claims on the same policy → feeds the "frequent claims" risk signal.
        var priorClaims = await _db.Claims.AsNoTracking()
            .CountAsync(x => x.PolicyId == c.PolicyId && x.Id != id, ct);
        var riskFlags = ClaimRiskRules.Evaluate(
            c.IncidentDate, c.PolicyEffectiveDate, c.ClaimedAmount, c.PolicySumInsured, priorClaims);

        Response = new ClaimDetailDto(
            c.Id, c.ClaimNo, c.PolicyId, c.PolicyNo, c.Status.ToString(),
            c.IncidentDate, c.Description, c.ClaimedAmount, c.ApprovedAmount, c.RejectReason,
            c.GarageId, c.GarageName, c.GaragePhone, c.SurveyorName, photos, riskFlags,
            new AuditInfo(c.CreatedUser, c.CreatedAt, c.UpdatedUser, c.UpdatedAt));
    }
}

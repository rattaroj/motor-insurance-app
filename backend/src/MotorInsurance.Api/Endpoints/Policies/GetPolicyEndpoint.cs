using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

public record PolicyDriverDto(string FullName, string NationalId, string IdCardImagePath);

public record EndorsementDto(
    string EndorsementNo, string FieldName, string? OldValue, string? NewValue,
    DateOnly EffectiveDate, string? Note, DateTime CreatedAt);

/// <summary>Policy detail = the list DTO plus named drivers and endorsement history.</summary>
public record PolicyDetailDto(
    long Id, string PolicyNo, long CustomerId, string CustomerName,
    long VehicleId, string VehicleRegistration, string Status, string CoverageType,
    decimal SumInsured, decimal Premium, decimal BasePremium, int NcbPercent, decimal Deductible,
    DateOnly? EffectiveDate, DateOnly? ExpiryDate,
    long? PreviousPolicyId,
    IReadOnlyList<string> Riders,
    IReadOnlyList<PolicyDriverDto> Drivers,
    IReadOnlyList<EndorsementDto> Endorsements);

/// <summary>GET /api/policies/{id}.</summary>
public class GetPolicyEndpoint : EndpointWithoutRequest<PolicyDetailDto>
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

        // Named drivers live on the originating quotation. A renewal has no quotation of its
        // own, so climb the PreviousPolicy chain to find the quotation that holds the drivers.
        var quotationId = await ResolveQuotationIdAsync(policy, ct);
        var drivers = quotationId is null
            ? new List<PolicyDriverDto>()
            : await _db.QuotationDrivers.AsNoTracking()
                .Where(d => d.QuotationId == quotationId)
                .OrderBy(d => d.Id)
                .Select(d => new PolicyDriverDto(d.FullName, d.NationalId, d.IdCardImagePath))
                .ToListAsync(ct);

        var endorsements = await _db.Endorsements.AsNoTracking()
            .Where(e => e.PolicyId == id)
            .OrderBy(e => e.Id)
            .Select(e => new EndorsementDto(
                e.EndorsementNo, e.FieldName, e.OldValue, e.NewValue, e.EffectiveDate, e.Note, e.CreatedAt))
            .ToListAsync(ct);

        var riders = await _db.PolicyRiders.AsNoTracking()
            .Where(pr => pr.PolicyId == id)
            .OrderBy(pr => pr.RiderId)
            .Select(pr => pr.Rider.Name)
            .ToListAsync(ct);

        Response = new PolicyDetailDto(
            policy.Id, policy.PolicyNo, policy.CustomerId, policy.Customer.FullName,
            policy.VehicleId, policy.Vehicle.RegistrationNo,
            policy.Status.ToString(), policy.CoverageType.ToString(),
            policy.SumInsured, policy.Premium, policy.BasePremium, policy.NcbPercent, policy.Deductible,
            policy.EffectiveDate, policy.ExpiryDate,
            policy.PreviousPolicyId, riders, drivers, endorsements);
    }

    private async Task<long?> ResolveQuotationIdAsync(Policy policy, CancellationToken ct)
    {
        if (policy.QuotationId is not null) return policy.QuotationId;

        var previousId = policy.PreviousPolicyId;
        while (previousId is not null)
        {
            var prev = await _db.Policies.AsNoTracking()
                .Where(p => p.Id == previousId)
                .Select(p => new { p.QuotationId, p.PreviousPolicyId })
                .FirstOrDefaultAsync(ct);
            if (prev is null) break;
            if (prev.QuotationId is not null) return prev.QuotationId;
            previousId = prev.PreviousPolicyId;
        }
        return null;
    }
}

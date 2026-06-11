using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Vehicles;

public record VehiclePolicyDto(
    long Id, string PolicyNo, string Status, string CoverageType, decimal Premium,
    DateOnly? EffectiveDate, DateOnly? ExpiryDate);

/// <summary>Vehicle detail = the list DTO (plus brand/model/submodel ids for the edit cascade) and policies.</summary>
public record VehicleDetailDto(
    long Id, long CustomerId, string CustomerName, string RegistrationNo, string Province,
    long ModelYearId, long BrandId, long ModelId, long SubmodelId,
    string Brand, string Model, string Submodel, Powertrain Powertrain,
    int Year, string? ChassisNo, IReadOnlyList<VehiclePolicyDto> Policies, AuditInfo Audit);

/// <summary>GET /api/vehicles/{id} — vehicle detail incl. the policies on this vehicle.</summary>
public class GetVehicleEndpoint : EndpointWithoutRequest<VehicleDetailDto>
{
    private readonly IAppDbContext _db;
    public GetVehicleEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("vehicles/{id}");
        Policies(PermissionPolicy.For(Perms.VehicleRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");

        var v = await _db.Vehicles.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.CustomerId,
                CustomerName = x.Customer.FullName,
                x.RegistrationNo,
                x.Province,
                x.ModelYearId,
                BrandId = x.ModelYear.Submodel.Model.BrandId,
                ModelId = x.ModelYear.Submodel.ModelId,
                SubmodelId = x.ModelYear.SubmodelId,
                Brand = x.ModelYear.Submodel.Model.Brand.Name,
                Model = x.ModelYear.Submodel.Model.Name,
                Submodel = x.ModelYear.Submodel.Name,
                x.ModelYear.Submodel.Powertrain,
                x.ModelYear.Year,
                x.ChassisNo,
                x.CreatedUser,
                x.CreatedAt,
                x.UpdatedUser,
                x.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Vehicle), id);

        var policies = await _db.Policies.AsNoTracking()
            .Where(p => p.VehicleId == id)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id, p.PolicyNo, p.Status, p.CoverageType, p.Premium, p.EffectiveDate, p.ExpiryDate,
            })
            .ToListAsync(ct);

        Response = new VehicleDetailDto(
            v.Id, v.CustomerId, v.CustomerName, v.RegistrationNo, v.Province, v.ModelYearId,
            v.BrandId, v.ModelId, v.SubmodelId,
            v.Brand, v.Model, v.Submodel, v.Powertrain, v.Year, v.ChassisNo,
            policies.Select(p => new VehiclePolicyDto(
                p.Id, p.PolicyNo, p.Status.ToString(), p.CoverageType.ToString(), p.Premium,
                p.EffectiveDate, p.ExpiryDate)).ToList(),
            new AuditInfo(v.CreatedUser, v.CreatedAt, v.UpdatedUser, v.UpdatedAt));
    }
}

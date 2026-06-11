using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Vehicles;

public record UpdateVehicleRequest(string RegistrationNo, string Province, long ModelYearId, string? ChassisNo);

public class UpdateVehicleValidator : Validator<UpdateVehicleRequest>
{
    public UpdateVehicleValidator()
    {
        RuleFor(x => x.RegistrationNo).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Province).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ModelYearId).GreaterThan(0);
        RuleFor(x => x.ChassisNo).MaximumLength(50);
    }
}

/// <summary>PUT /api/vehicles/{id} — update plate/province/model-year/chassis (owner is fixed).</summary>
public class UpdateVehicleEndpoint : Endpoint<UpdateVehicleRequest>
{
    private readonly IAppDbContext _db;
    public UpdateVehicleEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("vehicles/{id}");
        Policies(PermissionPolicy.For(Perms.VehicleWrite));
    }

    public override async Task HandleAsync(UpdateVehicleRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(Vehicle), id);

        if (await _db.VehicleModelYears.FindAsync(new object[] { r.ModelYearId }, ct) is null)
            throw new NotFoundException(nameof(VehicleModelYear), r.ModelYearId);

        vehicle.RegistrationNo = r.RegistrationNo;
        vehicle.Province = r.Province;
        vehicle.ModelYearId = r.ModelYearId;
        vehicle.ChassisNo = r.ChassisNo;

        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

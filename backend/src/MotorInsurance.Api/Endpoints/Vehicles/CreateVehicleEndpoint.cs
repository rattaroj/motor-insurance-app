using FastEndpoints;
using FluentValidation;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Vehicles;

public record CreateVehicleRequest(
    long CustomerId, string RegistrationNo, string Province, long ModelYearId, string? ChassisNo);
public record CreateVehicleResponse(long Id);

public class CreateVehicleValidator : Validator<CreateVehicleRequest>
{
    public CreateVehicleValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.RegistrationNo).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Province).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ModelYearId).GreaterThan(0);
        RuleFor(x => x.ChassisNo).MaximumLength(50);
    }
}

/// <summary>POST /api/vehicles.</summary>
public class CreateVehicleEndpoint : Endpoint<CreateVehicleRequest, CreateVehicleResponse>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public CreateVehicleEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Post("vehicles");
        Policies(PermissionPolicy.For(Perms.VehicleWrite));
    }

    public override async Task HandleAsync(CreateVehicleRequest r, CancellationToken ct)
    {
        if (await _db.Customers.FindAsync(new object[] { r.CustomerId }, ct) is null)
            throw new NotFoundException(nameof(Customer), r.CustomerId);

        if (await _db.VehicleModelYears.FindAsync(new object[] { r.ModelYearId }, ct) is null)
            throw new NotFoundException(nameof(VehicleModelYear), r.ModelYearId);

        var vehicle = new Vehicle
        {
            CustomerId = r.CustomerId,
            RegistrationNo = r.RegistrationNo,
            Province = r.Province,
            ModelYearId = r.ModelYearId,
            ChassisNo = r.ChassisNo,
            CreatedAt = _clock.UtcNow,
        };
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(ct);

        await Send.ResponseAsync(new CreateVehicleResponse(vehicle.Id), 201, ct);
    }
}

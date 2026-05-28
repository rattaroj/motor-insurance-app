using FluentValidation;
using MediatR;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Vehicles.Commands;

public record CreateVehicleCommand(
    long CustomerId,
    string RegistrationNo,
    string Province,
    long ModelYearId,
    string? ChassisNo = null) : IRequest<long>;

public class CreateVehicleValidator : AbstractValidator<CreateVehicleCommand>
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

public class CreateVehicleHandler : IRequestHandler<CreateVehicleCommand, long>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateVehicleHandler(IAppDbContext db, IDateTimeProvider clock)
        => (_db, _clock) = (db, clock);

    public async Task<long> Handle(CreateVehicleCommand req, CancellationToken ct)
    {
        var customerExists = await _db.Customers.FindAsync(new object[] { req.CustomerId }, ct) is not null;
        if (!customerExists) throw new NotFoundException(nameof(Customer), req.CustomerId);

        var modelYearExists = await _db.VehicleModelYears.FindAsync(new object[] { req.ModelYearId }, ct) is not null;
        if (!modelYearExists) throw new NotFoundException(nameof(VehicleModelYear), req.ModelYearId);

        var vehicle = new Vehicle
        {
            CustomerId = req.CustomerId,
            RegistrationNo = req.RegistrationNo,
            Province = req.Province,
            ModelYearId = req.ModelYearId,
            ChassisNo = req.ChassisNo,
            CreatedAt = _clock.UtcNow,
        };

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(ct);
        return vehicle.Id;
    }
}

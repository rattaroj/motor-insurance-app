using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Vehicles;

/// <summary>
/// DELETE /api/vehicles/{id} — hard-delete a vehicle, only when nothing references it
/// (no policy or quotation). Otherwise 409.
/// </summary>
public class DeleteVehicleEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteVehicleEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("vehicles/{id}");
        Policies(PermissionPolicy.For(Perms.VehicleWrite));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException(nameof(Vehicle), id);

        if (await _db.Policies.AnyAsync(p => p.VehicleId == id, ct))
            throw new ConflictException("Cannot delete a vehicle that has policies.");
        if (await _db.Quotations.AnyAsync(q => q.VehicleId == id, ct))
            throw new ConflictException("Cannot delete a vehicle that has quotations.");

        _db.Vehicles.Remove(vehicle);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

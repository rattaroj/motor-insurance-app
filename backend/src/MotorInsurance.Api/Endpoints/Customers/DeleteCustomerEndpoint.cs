using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Customers;

/// <summary>
/// DELETE /api/customers/{id} — hard-delete a customer, only when nothing references them
/// (no vehicle, quotation, or policy). Otherwise 409.
/// </summary>
public class DeleteCustomerEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteCustomerEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("customers/{id}");
        Policies(PermissionPolicy.For(Perms.CustomerWrite));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(Customer), id);

        if (await _db.Policies.AnyAsync(p => p.CustomerId == id, ct))
            throw new ConflictException("Cannot delete a customer that has policies.");
        if (await _db.Quotations.AnyAsync(q => q.CustomerId == id, ct))
            throw new ConflictException("Cannot delete a customer that has quotations.");
        if (await _db.Vehicles.AnyAsync(v => v.CustomerId == id, ct))
            throw new ConflictException("Cannot delete a customer that has vehicles.");

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public record CreateRiderRequest(string Name, decimal Premium);
public record UpdateRiderRequest(string Name, decimal Premium);

public class CreateRiderValidator : Validator<CreateRiderRequest>
{
    public CreateRiderValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Premium).GreaterThanOrEqualTo(0);
    }
}

public class UpdateRiderValidator : Validator<UpdateRiderRequest>
{
    public UpdateRiderValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Premium).GreaterThanOrEqualTo(0);
    }
}

/// <summary>GET /api/lookups/riders.</summary>
public class GetRidersEndpoint : EndpointWithoutRequest<IReadOnlyList<RiderDto>>
{
    private readonly IAppDbContext _db;
    public GetRidersEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/riders");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.Riders.AsNoTracking().OrderBy(r => r.Id)
            .Select(r => new RiderDto(r.Id, r.Name, r.Premium)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/riders.</summary>
public class CreateRiderEndpoint : Endpoint<CreateRiderRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateRiderEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/riders");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateRiderRequest r, CancellationToken ct)
    {
        if (await _db.Riders.AnyAsync(x => x.Name == r.Name, ct))
            throw new ConflictException($"Rider '{r.Name}' already exists.");
        var rider = new Rider { Name = r.Name, Premium = r.Premium };
        _db.Riders.Add(rider);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(rider.Id);
    }
}

/// <summary>PUT /api/lookups/riders/{id}.</summary>
public class UpdateRiderEndpoint : Endpoint<UpdateRiderRequest>
{
    private readonly IAppDbContext _db;
    public UpdateRiderEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/riders/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateRiderRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var rider = await _db.Riders.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Rider), id);
        if (await _db.Riders.AnyAsync(x => x.Name == r.Name && x.Id != id, ct))
            throw new ConflictException($"Rider '{r.Name}' already exists.");
        rider.Name = r.Name;
        rider.Premium = r.Premium;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/riders/{id}.</summary>
public class DeleteRiderEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteRiderEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/riders/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var rider = await _db.Riders.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Rider), id);
        if (await _db.QuotationRiders.AnyAsync(qr => qr.RiderId == id, ct) ||
            await _db.PolicyRiders.AnyAsync(pr => pr.RiderId == id, ct))
            throw new ConflictException("Cannot delete a rider that is in use by a quotation or policy.");
        _db.Riders.Remove(rider);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

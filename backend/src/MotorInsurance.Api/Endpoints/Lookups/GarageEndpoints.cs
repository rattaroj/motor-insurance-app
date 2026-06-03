using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public record CreateGarageRequest(string Name, string? Phone);
public record UpdateGarageRequest(string Name, string? Phone);

public class CreateGarageValidator : Validator<CreateGarageRequest>
{
    public CreateGarageValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}

public class UpdateGarageValidator : Validator<UpdateGarageRequest>
{
    public UpdateGarageValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}

/// <summary>GET /api/lookups/garages.</summary>
public class GetGaragesEndpoint : EndpointWithoutRequest<IReadOnlyList<GarageDto>>
{
    private readonly IAppDbContext _db;
    public GetGaragesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/garages");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.Garages.AsNoTracking().OrderBy(g => g.Name)
            .Select(g => new GarageDto(g.Id, g.Name, g.Phone)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/garages.</summary>
public class CreateGarageEndpoint : Endpoint<CreateGarageRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateGarageEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/garages");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateGarageRequest r, CancellationToken ct)
    {
        if (await _db.Garages.AnyAsync(g => g.Name == r.Name, ct))
            throw new ConflictException($"Garage '{r.Name}' already exists.");
        var garage = new Garage { Name = r.Name, Phone = r.Phone };
        _db.Garages.Add(garage);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(garage.Id);
    }
}

/// <summary>PUT /api/lookups/garages/{id}.</summary>
public class UpdateGarageEndpoint : Endpoint<UpdateGarageRequest>
{
    private readonly IAppDbContext _db;
    public UpdateGarageEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/garages/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateGarageRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var garage = await _db.Garages.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException(nameof(Garage), id);
        if (await _db.Garages.AnyAsync(g => g.Name == r.Name && g.Id != id, ct))
            throw new ConflictException($"Garage '{r.Name}' already exists.");
        garage.Name = r.Name;
        garage.Phone = r.Phone;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/garages/{id}.</summary>
public class DeleteGarageEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteGarageEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/garages/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var garage = await _db.Garages.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new NotFoundException(nameof(Garage), id);
        if (await _db.Claims.AnyAsync(c => c.GarageId == id, ct))
            throw new ConflictException("Cannot delete a garage that is assigned to a claim.");
        _db.Garages.Remove(garage);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

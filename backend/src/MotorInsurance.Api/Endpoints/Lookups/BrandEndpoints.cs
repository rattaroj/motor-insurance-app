using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public record CreateBrandRequest(string Name);
public record UpdateBrandRequest(string Name);

public class CreateBrandValidator : Validator<CreateBrandRequest>
{
    public CreateBrandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
}

public class UpdateBrandValidator : Validator<UpdateBrandRequest>
{
    public UpdateBrandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
}

/// <summary>GET /api/lookups/vehicle-brands.</summary>
public class GetBrandsEndpoint : EndpointWithoutRequest<IReadOnlyList<OptionDto>>
{
    private readonly IAppDbContext _db;
    public GetBrandsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/vehicle-brands");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.VehicleBrands.AsNoTracking().OrderBy(b => b.Name)
            .Select(b => new OptionDto(b.Id, b.Name)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/vehicle-brands.</summary>
public class CreateBrandEndpoint : Endpoint<CreateBrandRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateBrandEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/vehicle-brands");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateBrandRequest r, CancellationToken ct)
    {
        if (await _db.VehicleBrands.AnyAsync(b => b.Name == r.Name, ct))
            throw new ConflictException($"Brand '{r.Name}' already exists.");
        var brand = new VehicleBrand { Name = r.Name };
        _db.VehicleBrands.Add(brand);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(brand.Id);
    }
}

/// <summary>PUT /api/lookups/vehicle-brands/{id}.</summary>
public class UpdateBrandEndpoint : Endpoint<UpdateBrandRequest>
{
    private readonly IAppDbContext _db;
    public UpdateBrandEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/vehicle-brands/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateBrandRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var brand = await _db.VehicleBrands.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(VehicleBrand), id);
        if (await _db.VehicleBrands.AnyAsync(b => b.Name == r.Name && b.Id != id, ct))
            throw new ConflictException($"Brand '{r.Name}' already exists.");
        brand.Name = r.Name;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/vehicle-brands/{id}.</summary>
public class DeleteBrandEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteBrandEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/vehicle-brands/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var brand = await _db.VehicleBrands.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(VehicleBrand), id);
        if (await _db.VehicleModels.AnyAsync(m => m.BrandId == id, ct))
            throw new ConflictException("Cannot delete a brand that still has models.");
        _db.VehicleBrands.Remove(brand);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

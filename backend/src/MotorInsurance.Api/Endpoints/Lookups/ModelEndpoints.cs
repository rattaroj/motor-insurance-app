using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public class GetModelsRequest { public long BrandId { get; set; } }
public record CreateModelRequest(long BrandId, string Name);
public record UpdateModelRequest(string Name);

public class CreateModelValidator : Validator<CreateModelRequest>
{
    public CreateModelValidator()
    {
        RuleFor(x => x.BrandId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public class UpdateModelValidator : Validator<UpdateModelRequest>
{
    public UpdateModelValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
}

/// <summary>GET /api/lookups/vehicle-models?brandId=.</summary>
public class GetModelsEndpoint : Endpoint<GetModelsRequest, IReadOnlyList<OptionDto>>
{
    private readonly IAppDbContext _db;
    public GetModelsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/vehicle-models");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(GetModelsRequest r, CancellationToken ct) =>
        Response = await _db.VehicleModels.AsNoTracking().Where(m => m.BrandId == r.BrandId).OrderBy(m => m.Name)
            .Select(m => new OptionDto(m.Id, m.Name)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/vehicle-models.</summary>
public class CreateModelEndpoint : Endpoint<CreateModelRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateModelEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/vehicle-models");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateModelRequest r, CancellationToken ct)
    {
        if (await _db.VehicleBrands.FindAsync(new object[] { r.BrandId }, ct) is null)
            throw new NotFoundException(nameof(VehicleBrand), r.BrandId);
        if (await _db.VehicleModels.AnyAsync(m => m.BrandId == r.BrandId && m.Name == r.Name, ct))
            throw new ConflictException($"Model '{r.Name}' already exists for this brand.");
        var model = new VehicleModel { BrandId = r.BrandId, Name = r.Name };
        _db.VehicleModels.Add(model);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(model.Id);
    }
}

/// <summary>PUT /api/lookups/vehicle-models/{id}.</summary>
public class UpdateModelEndpoint : Endpoint<UpdateModelRequest>
{
    private readonly IAppDbContext _db;
    public UpdateModelEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/vehicle-models/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateModelRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var model = await _db.VehicleModels.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException(nameof(VehicleModel), id);
        if (await _db.VehicleModels.AnyAsync(m => m.BrandId == model.BrandId && m.Name == r.Name && m.Id != id, ct))
            throw new ConflictException($"Model '{r.Name}' already exists for this brand.");
        model.Name = r.Name;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/vehicle-models/{id}.</summary>
public class DeleteModelEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteModelEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/vehicle-models/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var model = await _db.VehicleModels.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException(nameof(VehicleModel), id);
        if (await _db.VehicleSubmodels.AnyAsync(s => s.ModelId == id, ct))
            throw new ConflictException("Cannot delete a model that still has submodels.");
        _db.VehicleModels.Remove(model);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

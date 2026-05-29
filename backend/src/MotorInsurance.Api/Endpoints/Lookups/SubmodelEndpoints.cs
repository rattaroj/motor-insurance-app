using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public class GetSubmodelsRequest { public long ModelId { get; set; } }
public record CreateSubmodelRequest(long ModelId, string Name, Powertrain Powertrain);
public record UpdateSubmodelRequest(string Name, Powertrain Powertrain);

public class CreateSubmodelValidator : Validator<CreateSubmodelRequest>
{
    public CreateSubmodelValidator()
    {
        RuleFor(x => x.ModelId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Powertrain).IsInEnum();
    }
}

public class UpdateSubmodelValidator : Validator<UpdateSubmodelRequest>
{
    public UpdateSubmodelValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Powertrain).IsInEnum();
    }
}

/// <summary>GET /api/lookups/vehicle-submodels?modelId=.</summary>
public class GetSubmodelsEndpoint : Endpoint<GetSubmodelsRequest, IReadOnlyList<SubmodelOptionDto>>
{
    private readonly IAppDbContext _db;
    public GetSubmodelsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/vehicle-submodels");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(GetSubmodelsRequest r, CancellationToken ct) =>
        Response = await _db.VehicleSubmodels.AsNoTracking().Where(s => s.ModelId == r.ModelId).OrderBy(s => s.Name)
            .Select(s => new SubmodelOptionDto(s.Id, s.Name, s.Powertrain)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/vehicle-submodels.</summary>
public class CreateSubmodelEndpoint : Endpoint<CreateSubmodelRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateSubmodelEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/vehicle-submodels");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateSubmodelRequest r, CancellationToken ct)
    {
        if (await _db.VehicleModels.FindAsync(new object[] { r.ModelId }, ct) is null)
            throw new NotFoundException(nameof(VehicleModel), r.ModelId);
        if (await _db.VehicleSubmodels.AnyAsync(s => s.ModelId == r.ModelId && s.Name == r.Name, ct))
            throw new ConflictException($"Submodel '{r.Name}' already exists for this model.");
        var submodel = new VehicleSubmodel { ModelId = r.ModelId, Name = r.Name, Powertrain = r.Powertrain };
        _db.VehicleSubmodels.Add(submodel);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(submodel.Id);
    }
}

/// <summary>PUT /api/lookups/vehicle-submodels/{id}.</summary>
public class UpdateSubmodelEndpoint : Endpoint<UpdateSubmodelRequest>
{
    private readonly IAppDbContext _db;
    public UpdateSubmodelEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/vehicle-submodels/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateSubmodelRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var submodel = await _db.VehicleSubmodels.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(VehicleSubmodel), id);
        if (await _db.VehicleSubmodels.AnyAsync(s => s.ModelId == submodel.ModelId && s.Name == r.Name && s.Id != id, ct))
            throw new ConflictException($"Submodel '{r.Name}' already exists for this model.");
        submodel.Name = r.Name;
        submodel.Powertrain = r.Powertrain;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/vehicle-submodels/{id}.</summary>
public class DeleteSubmodelEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteSubmodelEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/vehicle-submodels/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var submodel = await _db.VehicleSubmodels.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(VehicleSubmodel), id);
        if (await _db.VehicleModelYears.AnyAsync(y => y.SubmodelId == id, ct))
            throw new ConflictException("Cannot delete a submodel that still has model years.");
        _db.VehicleSubmodels.Remove(submodel);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

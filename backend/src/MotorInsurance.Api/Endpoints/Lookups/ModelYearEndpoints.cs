using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public class GetModelYearsRequest { public long SubmodelId { get; set; } }
public record CreateModelYearRequest(long SubmodelId, int Year);
public record UpdateModelYearRequest(int Year);

public class CreateModelYearValidator : Validator<CreateModelYearRequest>
{
    public CreateModelYearValidator()
    {
        RuleFor(x => x.SubmodelId).GreaterThan(0);
        RuleFor(x => x.Year).InclusiveBetween(1900, 2100);
    }
}

public class UpdateModelYearValidator : Validator<UpdateModelYearRequest>
{
    public UpdateModelYearValidator() => RuleFor(x => x.Year).InclusiveBetween(1900, 2100);
}

/// <summary>GET /api/lookups/vehicle-model-years?submodelId=.</summary>
public class GetModelYearsEndpoint : Endpoint<GetModelYearsRequest, IReadOnlyList<ModelYearOptionDto>>
{
    private readonly IAppDbContext _db;
    public GetModelYearsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/vehicle-model-years");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(GetModelYearsRequest r, CancellationToken ct) =>
        Response = await _db.VehicleModelYears.AsNoTracking().Where(y => y.SubmodelId == r.SubmodelId)
            .OrderByDescending(y => y.Year)
            .Select(y => new ModelYearOptionDto(y.Id, y.Year)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/vehicle-model-years.</summary>
public class CreateModelYearEndpoint : Endpoint<CreateModelYearRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateModelYearEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/vehicle-model-years");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateModelYearRequest r, CancellationToken ct)
    {
        if (await _db.VehicleSubmodels.FindAsync(new object[] { r.SubmodelId }, ct) is null)
            throw new NotFoundException(nameof(VehicleSubmodel), r.SubmodelId);
        if (await _db.VehicleModelYears.AnyAsync(y => y.SubmodelId == r.SubmodelId && y.Year == r.Year, ct))
            throw new ConflictException($"Year {r.Year} already exists for this submodel.");
        var year = new VehicleModelYear { SubmodelId = r.SubmodelId, Year = r.Year };
        _db.VehicleModelYears.Add(year);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(year.Id);
    }
}

/// <summary>PUT /api/lookups/vehicle-model-years/{id}.</summary>
public class UpdateModelYearEndpoint : Endpoint<UpdateModelYearRequest>
{
    private readonly IAppDbContext _db;
    public UpdateModelYearEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/vehicle-model-years/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateModelYearRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var year = await _db.VehicleModelYears.FirstOrDefaultAsync(y => y.Id == id, ct)
            ?? throw new NotFoundException(nameof(VehicleModelYear), id);
        if (await _db.VehicleModelYears.AnyAsync(y => y.SubmodelId == year.SubmodelId && y.Year == r.Year && y.Id != id, ct))
            throw new ConflictException($"Year {r.Year} already exists for this submodel.");
        year.Year = r.Year;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/vehicle-model-years/{id}.</summary>
public class DeleteModelYearEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteModelYearEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/vehicle-model-years/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var year = await _db.VehicleModelYears.FirstOrDefaultAsync(y => y.Id == id, ct)
            ?? throw new NotFoundException(nameof(VehicleModelYear), id);
        if (await _db.Vehicles.AnyAsync(v => v.ModelYearId == id, ct))
            throw new ConflictException("Cannot delete a model year that is used by a vehicle.");
        _db.VehicleModelYears.Remove(year);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

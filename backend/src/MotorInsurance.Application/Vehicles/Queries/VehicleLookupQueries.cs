using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Vehicles.Queries;

public record OptionDto(long Id, string Name);
public record SubmodelOptionDto(long Id, string Name, Powertrain Powertrain);
public record ModelYearOptionDto(long Id, int Year);

// ----- Brands -----
public record GetVehicleBrandsQuery : IRequest<IReadOnlyList<OptionDto>>;

public class GetVehicleBrandsHandler : IRequestHandler<GetVehicleBrandsQuery, IReadOnlyList<OptionDto>>
{
    private readonly IAppDbContext _db;
    public GetVehicleBrandsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OptionDto>> Handle(GetVehicleBrandsQuery req, CancellationToken ct) =>
        await _db.VehicleBrands.AsNoTracking().OrderBy(b => b.Name)
            .Select(b => new OptionDto(b.Id, b.Name)).ToListAsync(ct);
}

// ----- Models by brand -----
public record GetVehicleModelsQuery(long BrandId) : IRequest<IReadOnlyList<OptionDto>>;

public class GetVehicleModelsHandler : IRequestHandler<GetVehicleModelsQuery, IReadOnlyList<OptionDto>>
{
    private readonly IAppDbContext _db;
    public GetVehicleModelsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OptionDto>> Handle(GetVehicleModelsQuery req, CancellationToken ct) =>
        await _db.VehicleModels.AsNoTracking().Where(m => m.BrandId == req.BrandId).OrderBy(m => m.Name)
            .Select(m => new OptionDto(m.Id, m.Name)).ToListAsync(ct);
}

// ----- Submodels by model -----
public record GetVehicleSubmodelsQuery(long ModelId) : IRequest<IReadOnlyList<SubmodelOptionDto>>;

public class GetVehicleSubmodelsHandler : IRequestHandler<GetVehicleSubmodelsQuery, IReadOnlyList<SubmodelOptionDto>>
{
    private readonly IAppDbContext _db;
    public GetVehicleSubmodelsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SubmodelOptionDto>> Handle(GetVehicleSubmodelsQuery req, CancellationToken ct) =>
        await _db.VehicleSubmodels.AsNoTracking().Where(s => s.ModelId == req.ModelId).OrderBy(s => s.Name)
            .Select(s => new SubmodelOptionDto(s.Id, s.Name, s.Powertrain)).ToListAsync(ct);
}

// ----- Years by submodel -----
public record GetVehicleModelYearsQuery(long SubmodelId) : IRequest<IReadOnlyList<ModelYearOptionDto>>;

public class GetVehicleModelYearsHandler
    : IRequestHandler<GetVehicleModelYearsQuery, IReadOnlyList<ModelYearOptionDto>>
{
    private readonly IAppDbContext _db;
    public GetVehicleModelYearsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ModelYearOptionDto>> Handle(GetVehicleModelYearsQuery req, CancellationToken ct) =>
        await _db.VehicleModelYears.AsNoTracking().Where(y => y.SubmodelId == req.SubmodelId)
            .OrderByDescending(y => y.Year)
            .Select(y => new ModelYearOptionDto(y.Id, y.Year)).ToListAsync(ct);
}

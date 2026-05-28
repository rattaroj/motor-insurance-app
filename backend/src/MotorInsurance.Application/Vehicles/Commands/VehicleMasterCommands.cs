using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Vehicles.Commands;

// ============================================================
// Vehicle master data CRUD: brand -> model -> submodel -> year
// Each delete is guarded against orphaning children / in-use rows.
// ============================================================

// ----- Brand -----
public record CreateBrandCommand(string Name) : IRequest<long>;
public record UpdateBrandCommand(long Id, string Name) : IRequest;
public record DeleteBrandCommand(long Id) : IRequest;

public class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
}

public class UpdateBrandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public class CreateBrandHandler : IRequestHandler<CreateBrandCommand, long>
{
    private readonly IAppDbContext _db;
    public CreateBrandHandler(IAppDbContext db) => _db = db;

    public async Task<long> Handle(CreateBrandCommand req, CancellationToken ct)
    {
        if (await _db.VehicleBrands.AnyAsync(b => b.Name == req.Name, ct))
            throw new ConflictException($"Brand '{req.Name}' already exists.");
        var brand = new VehicleBrand { Name = req.Name };
        _db.VehicleBrands.Add(brand);
        await _db.SaveChangesAsync(ct);
        return brand.Id;
    }
}

public class UpdateBrandHandler : IRequestHandler<UpdateBrandCommand>
{
    private readonly IAppDbContext _db;
    public UpdateBrandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(UpdateBrandCommand req, CancellationToken ct)
    {
        var brand = await _db.VehicleBrands.FirstOrDefaultAsync(b => b.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(VehicleBrand), req.Id);
        if (await _db.VehicleBrands.AnyAsync(b => b.Name == req.Name && b.Id != req.Id, ct))
            throw new ConflictException($"Brand '{req.Name}' already exists.");
        brand.Name = req.Name;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteBrandHandler : IRequestHandler<DeleteBrandCommand>
{
    private readonly IAppDbContext _db;
    public DeleteBrandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DeleteBrandCommand req, CancellationToken ct)
    {
        var brand = await _db.VehicleBrands.FirstOrDefaultAsync(b => b.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(VehicleBrand), req.Id);
        if (await _db.VehicleModels.AnyAsync(m => m.BrandId == req.Id, ct))
            throw new ConflictException("Cannot delete a brand that still has models.");
        _db.VehicleBrands.Remove(brand);
        await _db.SaveChangesAsync(ct);
    }
}

// ----- Model -----
public record CreateModelCommand(long BrandId, string Name) : IRequest<long>;
public record UpdateModelCommand(long Id, string Name) : IRequest;
public record DeleteModelCommand(long Id) : IRequest;

public class CreateModelValidator : AbstractValidator<CreateModelCommand>
{
    public CreateModelValidator()
    {
        RuleFor(x => x.BrandId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public class UpdateModelValidator : AbstractValidator<UpdateModelCommand>
{
    public UpdateModelValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public class CreateModelHandler : IRequestHandler<CreateModelCommand, long>
{
    private readonly IAppDbContext _db;
    public CreateModelHandler(IAppDbContext db) => _db = db;

    public async Task<long> Handle(CreateModelCommand req, CancellationToken ct)
    {
        if (await _db.VehicleBrands.FindAsync(new object[] { req.BrandId }, ct) is null)
            throw new NotFoundException(nameof(VehicleBrand), req.BrandId);
        if (await _db.VehicleModels.AnyAsync(m => m.BrandId == req.BrandId && m.Name == req.Name, ct))
            throw new ConflictException($"Model '{req.Name}' already exists for this brand.");
        var model = new VehicleModel { BrandId = req.BrandId, Name = req.Name };
        _db.VehicleModels.Add(model);
        await _db.SaveChangesAsync(ct);
        return model.Id;
    }
}

public class UpdateModelHandler : IRequestHandler<UpdateModelCommand>
{
    private readonly IAppDbContext _db;
    public UpdateModelHandler(IAppDbContext db) => _db = db;

    public async Task Handle(UpdateModelCommand req, CancellationToken ct)
    {
        var model = await _db.VehicleModels.FirstOrDefaultAsync(m => m.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(VehicleModel), req.Id);
        if (await _db.VehicleModels.AnyAsync(m => m.BrandId == model.BrandId && m.Name == req.Name && m.Id != req.Id, ct))
            throw new ConflictException($"Model '{req.Name}' already exists for this brand.");
        model.Name = req.Name;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteModelHandler : IRequestHandler<DeleteModelCommand>
{
    private readonly IAppDbContext _db;
    public DeleteModelHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DeleteModelCommand req, CancellationToken ct)
    {
        var model = await _db.VehicleModels.FirstOrDefaultAsync(m => m.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(VehicleModel), req.Id);
        if (await _db.VehicleSubmodels.AnyAsync(s => s.ModelId == req.Id, ct))
            throw new ConflictException("Cannot delete a model that still has submodels.");
        _db.VehicleModels.Remove(model);
        await _db.SaveChangesAsync(ct);
    }
}

// ----- Submodel -----
public record CreateSubmodelCommand(long ModelId, string Name) : IRequest<long>;
public record UpdateSubmodelCommand(long Id, string Name) : IRequest;
public record DeleteSubmodelCommand(long Id) : IRequest;

public class CreateSubmodelValidator : AbstractValidator<CreateSubmodelCommand>
{
    public CreateSubmodelValidator()
    {
        RuleFor(x => x.ModelId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public class UpdateSubmodelValidator : AbstractValidator<UpdateSubmodelCommand>
{
    public UpdateSubmodelValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}

public class CreateSubmodelHandler : IRequestHandler<CreateSubmodelCommand, long>
{
    private readonly IAppDbContext _db;
    public CreateSubmodelHandler(IAppDbContext db) => _db = db;

    public async Task<long> Handle(CreateSubmodelCommand req, CancellationToken ct)
    {
        if (await _db.VehicleModels.FindAsync(new object[] { req.ModelId }, ct) is null)
            throw new NotFoundException(nameof(VehicleModel), req.ModelId);
        if (await _db.VehicleSubmodels.AnyAsync(s => s.ModelId == req.ModelId && s.Name == req.Name, ct))
            throw new ConflictException($"Submodel '{req.Name}' already exists for this model.");
        var submodel = new VehicleSubmodel { ModelId = req.ModelId, Name = req.Name };
        _db.VehicleSubmodels.Add(submodel);
        await _db.SaveChangesAsync(ct);
        return submodel.Id;
    }
}

public class UpdateSubmodelHandler : IRequestHandler<UpdateSubmodelCommand>
{
    private readonly IAppDbContext _db;
    public UpdateSubmodelHandler(IAppDbContext db) => _db = db;

    public async Task Handle(UpdateSubmodelCommand req, CancellationToken ct)
    {
        var submodel = await _db.VehicleSubmodels.FirstOrDefaultAsync(s => s.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(VehicleSubmodel), req.Id);
        if (await _db.VehicleSubmodels.AnyAsync(s => s.ModelId == submodel.ModelId && s.Name == req.Name && s.Id != req.Id, ct))
            throw new ConflictException($"Submodel '{req.Name}' already exists for this model.");
        submodel.Name = req.Name;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteSubmodelHandler : IRequestHandler<DeleteSubmodelCommand>
{
    private readonly IAppDbContext _db;
    public DeleteSubmodelHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DeleteSubmodelCommand req, CancellationToken ct)
    {
        var submodel = await _db.VehicleSubmodels.FirstOrDefaultAsync(s => s.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(VehicleSubmodel), req.Id);
        if (await _db.VehicleModelYears.AnyAsync(y => y.SubmodelId == req.Id, ct))
            throw new ConflictException("Cannot delete a submodel that still has model years.");
        _db.VehicleSubmodels.Remove(submodel);
        await _db.SaveChangesAsync(ct);
    }
}

// ----- Model year -----
public record CreateModelYearCommand(long SubmodelId, int Year) : IRequest<long>;
public record UpdateModelYearCommand(long Id, int Year) : IRequest;
public record DeleteModelYearCommand(long Id) : IRequest;

public class CreateModelYearValidator : AbstractValidator<CreateModelYearCommand>
{
    public CreateModelYearValidator()
    {
        RuleFor(x => x.SubmodelId).GreaterThan(0);
        RuleFor(x => x.Year).InclusiveBetween(1900, 2100);
    }
}

public class UpdateModelYearValidator : AbstractValidator<UpdateModelYearCommand>
{
    public UpdateModelYearValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Year).InclusiveBetween(1900, 2100);
    }
}

public class CreateModelYearHandler : IRequestHandler<CreateModelYearCommand, long>
{
    private readonly IAppDbContext _db;
    public CreateModelYearHandler(IAppDbContext db) => _db = db;

    public async Task<long> Handle(CreateModelYearCommand req, CancellationToken ct)
    {
        if (await _db.VehicleSubmodels.FindAsync(new object[] { req.SubmodelId }, ct) is null)
            throw new NotFoundException(nameof(VehicleSubmodel), req.SubmodelId);
        if (await _db.VehicleModelYears.AnyAsync(y => y.SubmodelId == req.SubmodelId && y.Year == req.Year, ct))
            throw new ConflictException($"Year {req.Year} already exists for this submodel.");
        var year = new VehicleModelYear { SubmodelId = req.SubmodelId, Year = req.Year };
        _db.VehicleModelYears.Add(year);
        await _db.SaveChangesAsync(ct);
        return year.Id;
    }
}

public class UpdateModelYearHandler : IRequestHandler<UpdateModelYearCommand>
{
    private readonly IAppDbContext _db;
    public UpdateModelYearHandler(IAppDbContext db) => _db = db;

    public async Task Handle(UpdateModelYearCommand req, CancellationToken ct)
    {
        var year = await _db.VehicleModelYears.FirstOrDefaultAsync(y => y.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(VehicleModelYear), req.Id);
        if (await _db.VehicleModelYears.AnyAsync(y => y.SubmodelId == year.SubmodelId && y.Year == req.Year && y.Id != req.Id, ct))
            throw new ConflictException($"Year {req.Year} already exists for this submodel.");
        year.Year = req.Year;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteModelYearHandler : IRequestHandler<DeleteModelYearCommand>
{
    private readonly IAppDbContext _db;
    public DeleteModelYearHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DeleteModelYearCommand req, CancellationToken ct)
    {
        var year = await _db.VehicleModelYears.FirstOrDefaultAsync(y => y.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(VehicleModelYear), req.Id);
        if (await _db.Vehicles.AnyAsync(v => v.ModelYearId == req.Id, ct))
            throw new ConflictException("Cannot delete a model year that is used by a vehicle.");
        _db.VehicleModelYears.Remove(year);
        await _db.SaveChangesAsync(ct);
    }
}

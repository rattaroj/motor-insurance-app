using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

// Id is the official province code, supplied by the caller on create.
public record CreateProvinceRequest(long Id, string NameTh, string NameEn);
public record UpdateProvinceRequest(string NameTh, string NameEn);

public class CreateProvinceValidator : Validator<CreateProvinceRequest>
{
    public CreateProvinceValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameTh).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(150);
    }
}

public class UpdateProvinceValidator : Validator<UpdateProvinceRequest>
{
    public UpdateProvinceValidator()
    {
        RuleFor(x => x.NameTh).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(150);
    }
}

/// <summary>GET /api/lookups/provinces.</summary>
public class GetProvincesEndpoint : EndpointWithoutRequest<IReadOnlyList<GeoOptionDto>>
{
    private readonly IAppDbContext _db;
    public GetProvincesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/provinces");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.Provinces.AsNoTracking().OrderBy(p => p.NameTh)
            .Select(p => new GeoOptionDto(p.Id, p.NameTh, p.NameEn)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/provinces.</summary>
public class CreateProvinceEndpoint : Endpoint<CreateProvinceRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateProvinceEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/provinces");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateProvinceRequest r, CancellationToken ct)
    {
        if (await _db.Provinces.AnyAsync(p => p.Id == r.Id, ct))
            throw new ConflictException($"Province with code '{r.Id}' already exists.");
        var province = new Province { Id = r.Id, NameTh = r.NameTh, NameEn = r.NameEn };
        _db.Provinces.Add(province);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(province.Id);
    }
}

/// <summary>PUT /api/lookups/provinces/{id}.</summary>
public class UpdateProvinceEndpoint : Endpoint<UpdateProvinceRequest>
{
    private readonly IAppDbContext _db;
    public UpdateProvinceEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/provinces/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateProvinceRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var province = await _db.Provinces.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Province), id);
        province.NameTh = r.NameTh;
        province.NameEn = r.NameEn;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/provinces/{id}.</summary>
public class DeleteProvinceEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteProvinceEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/provinces/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var province = await _db.Provinces.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(Province), id);
        if (await _db.Districts.AnyAsync(d => d.ProvinceId == id, ct))
            throw new ConflictException("Cannot delete a province that still has districts.");
        _db.Provinces.Remove(province);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

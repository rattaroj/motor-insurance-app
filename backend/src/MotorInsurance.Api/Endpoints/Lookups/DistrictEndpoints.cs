using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public class GetDistrictsRequest { public long ProvinceId { get; set; } }
// Id is the official district (amphoe) code, supplied by the caller on create.
public record CreateDistrictRequest(long Id, long ProvinceId, string NameTh, string NameEn);
public record UpdateDistrictRequest(string NameTh, string NameEn);

public class CreateDistrictValidator : Validator<CreateDistrictRequest>
{
    public CreateDistrictValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ProvinceId).GreaterThan(0);
        RuleFor(x => x.NameTh).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(150);
    }
}

public class UpdateDistrictValidator : Validator<UpdateDistrictRequest>
{
    public UpdateDistrictValidator()
    {
        RuleFor(x => x.NameTh).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(150);
    }
}

/// <summary>GET /api/lookups/districts?provinceId=.</summary>
public class GetDistrictsEndpoint : Endpoint<GetDistrictsRequest, IReadOnlyList<GeoOptionDto>>
{
    private readonly IAppDbContext _db;
    public GetDistrictsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/districts");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(GetDistrictsRequest r, CancellationToken ct) =>
        Response = await _db.Districts.AsNoTracking().Where(d => d.ProvinceId == r.ProvinceId).OrderBy(d => d.NameTh)
            .Select(d => new GeoOptionDto(d.Id, d.NameTh, d.NameEn)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/districts.</summary>
public class CreateDistrictEndpoint : Endpoint<CreateDistrictRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateDistrictEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/districts");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateDistrictRequest r, CancellationToken ct)
    {
        if (await _db.Provinces.FindAsync(new object[] { r.ProvinceId }, ct) is null)
            throw new NotFoundException(nameof(Province), r.ProvinceId);
        if (await _db.Districts.AnyAsync(d => d.Id == r.Id, ct))
            throw new ConflictException($"District with code '{r.Id}' already exists.");
        var district = new District { Id = r.Id, ProvinceId = r.ProvinceId, NameTh = r.NameTh, NameEn = r.NameEn };
        _db.Districts.Add(district);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(district.Id);
    }
}

/// <summary>PUT /api/lookups/districts/{id}.</summary>
public class UpdateDistrictEndpoint : Endpoint<UpdateDistrictRequest>
{
    private readonly IAppDbContext _db;
    public UpdateDistrictEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/districts/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateDistrictRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var district = await _db.Districts.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException(nameof(District), id);
        district.NameTh = r.NameTh;
        district.NameEn = r.NameEn;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/districts/{id}.</summary>
public class DeleteDistrictEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteDistrictEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/districts/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var district = await _db.Districts.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException(nameof(District), id);
        if (await _db.Subdistricts.AnyAsync(s => s.DistrictId == id, ct))
            throw new ConflictException("Cannot delete a district that still has subdistricts.");
        _db.Districts.Remove(district);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

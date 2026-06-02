using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public class GetSubdistrictsRequest { public long DistrictId { get; set; } }
// Id is the official subdistrict (tambon) code, supplied by the caller on create.
public record CreateSubdistrictRequest(long Id, long DistrictId, long PostalCodeId, string NameTh, string NameEn);
public record UpdateSubdistrictRequest(string NameTh, string NameEn, long PostalCodeId);

public class CreateSubdistrictValidator : Validator<CreateSubdistrictRequest>
{
    public CreateSubdistrictValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.DistrictId).GreaterThan(0);
        RuleFor(x => x.PostalCodeId).GreaterThan(0);
        RuleFor(x => x.NameTh).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(150);
    }
}

public class UpdateSubdistrictValidator : Validator<UpdateSubdistrictRequest>
{
    public UpdateSubdistrictValidator()
    {
        RuleFor(x => x.PostalCodeId).GreaterThan(0);
        RuleFor(x => x.NameTh).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(150);
    }
}

/// <summary>GET /api/lookups/subdistricts?districtId= (each carries its postal code for auto-fill).</summary>
public class GetSubdistrictsEndpoint : Endpoint<GetSubdistrictsRequest, IReadOnlyList<SubdistrictOptionDto>>
{
    private readonly IAppDbContext _db;
    public GetSubdistrictsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/subdistricts");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(GetSubdistrictsRequest r, CancellationToken ct) =>
        Response = await _db.Subdistricts.AsNoTracking().Where(s => s.DistrictId == r.DistrictId).OrderBy(s => s.NameTh)
            .Select(s => new SubdistrictOptionDto(s.Id, s.NameTh, s.NameEn, s.PostalCodeId, s.PostalCode.Code))
            .ToListAsync(ct);
}

/// <summary>POST /api/lookups/subdistricts.</summary>
public class CreateSubdistrictEndpoint : Endpoint<CreateSubdistrictRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateSubdistrictEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/subdistricts");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateSubdistrictRequest r, CancellationToken ct)
    {
        if (await _db.Districts.FindAsync(new object[] { r.DistrictId }, ct) is null)
            throw new NotFoundException(nameof(District), r.DistrictId);
        if (await _db.PostalCodes.FindAsync(new object[] { r.PostalCodeId }, ct) is null)
            throw new NotFoundException(nameof(PostalCode), r.PostalCodeId);
        if (await _db.Subdistricts.AnyAsync(s => s.Id == r.Id, ct))
            throw new ConflictException($"Subdistrict with code '{r.Id}' already exists.");
        var subdistrict = new Subdistrict
        {
            Id = r.Id,
            DistrictId = r.DistrictId,
            PostalCodeId = r.PostalCodeId,
            NameTh = r.NameTh,
            NameEn = r.NameEn,
        };
        _db.Subdistricts.Add(subdistrict);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(subdistrict.Id);
    }
}

/// <summary>PUT /api/lookups/subdistricts/{id}.</summary>
public class UpdateSubdistrictEndpoint : Endpoint<UpdateSubdistrictRequest>
{
    private readonly IAppDbContext _db;
    public UpdateSubdistrictEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/subdistricts/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateSubdistrictRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var subdistrict = await _db.Subdistricts.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(Subdistrict), id);
        if (await _db.PostalCodes.FindAsync(new object[] { r.PostalCodeId }, ct) is null)
            throw new NotFoundException(nameof(PostalCode), r.PostalCodeId);
        subdistrict.NameTh = r.NameTh;
        subdistrict.NameEn = r.NameEn;
        subdistrict.PostalCodeId = r.PostalCodeId;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/subdistricts/{id}.</summary>
public class DeleteSubdistrictEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteSubdistrictEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/subdistricts/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var subdistrict = await _db.Subdistricts.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException(nameof(Subdistrict), id);
        _db.Subdistricts.Remove(subdistrict);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

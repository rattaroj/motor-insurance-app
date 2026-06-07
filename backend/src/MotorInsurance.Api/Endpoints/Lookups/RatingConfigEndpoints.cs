using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

// ----- Age-loading bands -----

public record AgeLoadingBandDto(long Id, int? MaxAge, decimal Surcharge, DateOnly EffectiveDate);
public record CreateAgeBandRequest(int? MaxAge, decimal Surcharge, DateOnly EffectiveDate);
public record UpdateAgeBandRequest(int? MaxAge, decimal Surcharge);

public class CreateAgeBandValidator : Validator<CreateAgeBandRequest>
{
    public CreateAgeBandValidator()
    {
        RuleFor(x => x.Surcharge).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1m);
        RuleFor(x => x.MaxAge).GreaterThanOrEqualTo(0).When(x => x.MaxAge is not null);
    }
}

public class UpdateAgeBandValidator : Validator<UpdateAgeBandRequest>
{
    public UpdateAgeBandValidator()
    {
        RuleFor(x => x.Surcharge).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1m);
        RuleFor(x => x.MaxAge).GreaterThanOrEqualTo(0).When(x => x.MaxAge is not null);
    }
}

/// <summary>GET /api/lookups/age-loading-bands — configurable vehicle-age surcharge bands.</summary>
public class GetAgeBandsEndpoint : EndpointWithoutRequest<IReadOnlyList<AgeLoadingBandDto>>
{
    private readonly IAppDbContext _db;
    public GetAgeBandsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/age-loading-bands");
        Policies(PermissionPolicy.For(Perms.RatingRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.AgeLoadingBands.AsNoTracking()
            .OrderByDescending(b => b.EffectiveDate).ThenBy(b => b.MaxAge == null).ThenBy(b => b.MaxAge)
            .Select(b => new AgeLoadingBandDto(b.Id, b.MaxAge, b.Surcharge, b.EffectiveDate))
            .ToListAsync(ct);
}

/// <summary>POST /api/lookups/age-loading-bands.</summary>
public class CreateAgeBandEndpoint : Endpoint<CreateAgeBandRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateAgeBandEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/age-loading-bands");
        Policies(PermissionPolicy.For(Perms.RatingManage));
    }

    public override async Task HandleAsync(CreateAgeBandRequest r, CancellationToken ct)
    {
        var band = new AgeLoadingBand { MaxAge = r.MaxAge, Surcharge = r.Surcharge, EffectiveDate = r.EffectiveDate };
        _db.AgeLoadingBands.Add(band);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(band.Id);
    }
}

/// <summary>PUT /api/lookups/age-loading-bands/{id}.</summary>
public class UpdateAgeBandEndpoint : Endpoint<UpdateAgeBandRequest>
{
    private readonly IAppDbContext _db;
    public UpdateAgeBandEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/age-loading-bands/{id}");
        Policies(PermissionPolicy.For(Perms.RatingManage));
    }

    public override async Task HandleAsync(UpdateAgeBandRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var band = await _db.AgeLoadingBands.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(AgeLoadingBand), id);
        band.MaxAge = r.MaxAge;
        band.Surcharge = r.Surcharge;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/age-loading-bands/{id}.</summary>
public class DeleteAgeBandEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteAgeBandEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/age-loading-bands/{id}");
        Policies(PermissionPolicy.For(Perms.RatingManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var band = await _db.AgeLoadingBands.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException(nameof(AgeLoadingBand), id);
        _db.AgeLoadingBands.Remove(band);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

// ----- Rating settings -----

public record RatingSettingDto(string Code, decimal Value);
public record UpdateRatingSettingRequest(decimal Value);

public class UpdateRatingSettingValidator : Validator<UpdateRatingSettingRequest>
{
    public UpdateRatingSettingValidator() =>
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1m);
}

/// <summary>GET /api/lookups/rating-settings — tunable scalar rating settings.</summary>
public class GetRatingSettingsEndpoint : EndpointWithoutRequest<IReadOnlyList<RatingSettingDto>>
{
    private readonly IAppDbContext _db;
    public GetRatingSettingsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/rating-settings");
        Policies(PermissionPolicy.For(Perms.RatingRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.RatingSettings.AsNoTracking()
            .OrderBy(s => s.Code)
            .Select(s => new RatingSettingDto(s.Code, s.Value))
            .ToListAsync(ct);
}

/// <summary>PUT /api/lookups/rating-settings/{code}.</summary>
public class UpdateRatingSettingEndpoint : Endpoint<UpdateRatingSettingRequest>
{
    private readonly IAppDbContext _db;
    public UpdateRatingSettingEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/rating-settings/{code}");
        Policies(PermissionPolicy.For(Perms.RatingManage));
    }

    public override async Task HandleAsync(UpdateRatingSettingRequest r, CancellationToken ct)
    {
        var code = Route<string>("code")!;
        var setting = await _db.RatingSettings.FirstOrDefaultAsync(s => s.Code == code, ct)
            ?? throw new NotFoundException(nameof(RatingSetting), code);
        setting.Value = r.Value;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

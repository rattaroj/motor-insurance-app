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

public record PremiumRateDto(long Id, string Coverage, decimal Rate, DateOnly EffectiveDate);
public record UpdatePremiumRateRequest(decimal Rate);
public record CreatePremiumRateRequest(CoverageType Coverage, decimal Rate, DateOnly EffectiveDate);

public class UpdatePremiumRateValidator : Validator<UpdatePremiumRateRequest>
{
    public UpdatePremiumRateValidator() =>
        RuleFor(x => x.Rate).GreaterThan(0).LessThanOrEqualTo(1m)
            .WithMessage("อัตราเบี้ยต้องอยู่ระหว่าง 0 ถึง 1 (เช่น 0.045)");
}

public class CreatePremiumRateValidator : Validator<CreatePremiumRateRequest>
{
    public CreatePremiumRateValidator() =>
        RuleFor(x => x.Rate).GreaterThan(0).LessThanOrEqualTo(1m)
            .WithMessage("อัตราเบี้ยต้องอยู่ระหว่าง 0 ถึง 1 (เช่น 0.045)");
}

/// <summary>GET /api/lookups/premium-rates — configurable base rates (effective-dated) per coverage.</summary>
public class GetPremiumRatesEndpoint : EndpointWithoutRequest<IReadOnlyList<PremiumRateDto>>
{
    private readonly IAppDbContext _db;
    public GetPremiumRatesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/premium-rates");
        Policies(PermissionPolicy.For(Perms.RatingRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.PremiumRates.AsNoTracking()
            .OrderBy(r => r.Coverage).ThenByDescending(r => r.EffectiveDate)
            .Select(r => new PremiumRateDto(r.Id, r.Coverage.ToString(), r.Rate, r.EffectiveDate))
            .ToListAsync(ct);
}

/// <summary>POST /api/lookups/premium-rates — add a new effective-dated rate for a coverage type.</summary>
public class CreatePremiumRateEndpoint : Endpoint<CreatePremiumRateRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreatePremiumRateEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/premium-rates");
        Policies(PermissionPolicy.For(Perms.RatingManage));
    }

    public override async Task HandleAsync(CreatePremiumRateRequest r, CancellationToken ct)
    {
        if (await _db.PremiumRates.AnyAsync(x => x.Coverage == r.Coverage && x.EffectiveDate == r.EffectiveDate, ct))
            throw new ConflictException("มีอัตราของชั้นนี้ ณ วันที่มีผลนี้อยู่แล้ว");
        var rate = new PremiumRate { Coverage = r.Coverage, Rate = r.Rate, EffectiveDate = r.EffectiveDate };
        _db.PremiumRates.Add(rate);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(rate.Id);
    }
}

/// <summary>PUT /api/lookups/premium-rates/{id} — update one rate row's value.</summary>
public class UpdatePremiumRateEndpoint : Endpoint<UpdatePremiumRateRequest>
{
    private readonly IAppDbContext _db;
    public UpdatePremiumRateEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/premium-rates/{id}");
        Policies(PermissionPolicy.For(Perms.RatingManage));
    }

    public override async Task HandleAsync(UpdatePremiumRateRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var rate = await _db.PremiumRates.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(PremiumRate), id);
        rate.Rate = r.Rate;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

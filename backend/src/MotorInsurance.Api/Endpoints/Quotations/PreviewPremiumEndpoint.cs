using FastEndpoints;
using FluentValidation;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Quotations;

public record PreviewPremiumRequest(
    long VehicleId, CoverageType CoverageType, decimal SumInsured,
    int NcbPercent = 0, decimal Deductible = 0, IReadOnlyList<long>? RiderIds = null);

public record PremiumBreakdownDto(
    decimal BasePremium, decimal VehicleAgeLoading, decimal NcbDiscount,
    decimal DeductibleDiscount, decimal RidersTotal, decimal NetPremium);

public class PreviewPremiumValidator : Validator<PreviewPremiumRequest>
{
    public PreviewPremiumValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.SumInsured).GreaterThan(0).LessThanOrEqualTo(50_000_000);
        RuleFor(x => x.NcbPercent).Must(PremiumCalculator.NcbSteps.Contains)
            .WithMessage("ส่วนลดประวัติดีต้องเป็น 0/20/30/40/50%");
        RuleFor(x => x.Deductible).GreaterThanOrEqualTo(0);
    }
}

/// <summary>POST /api/quotations/preview — rate the premium WITHOUT persisting (live form preview).</summary>
public class PreviewPremiumEndpoint : Endpoint<PreviewPremiumRequest, PremiumBreakdownDto>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public PreviewPremiumEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Post("quotations/preview");
        Policies(PermissionPolicy.For(Perms.QuotationWrite));
    }

    public override async Task HandleAsync(PreviewPremiumRequest r, CancellationToken ct)
    {
        var rating = await PremiumRatingService.RateAsync(
            _db, _clock.UtcNow.Year, r.VehicleId, r.CoverageType, r.SumInsured,
            r.NcbPercent, r.Deductible, r.RiderIds, ct);
        var b = rating.Breakdown;
        Response = new PremiumBreakdownDto(
            b.BasePremium, b.VehicleAgeLoading, b.NcbDiscount, b.DeductibleDiscount, b.RidersTotal, b.NetPremium);
    }
}

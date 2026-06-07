using FastEndpoints;
using FluentValidation;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Quotations;

public record CompareCoverageRequest(
    long VehicleId, decimal SumInsured,
    int NcbPercent = 0, decimal Deductible = 0, IReadOnlyList<long>? RiderIds = null);

public record CoverageOptionDto(string CoverageType, PremiumBreakdownDto Breakdown);
public record CompareCoverageResponse(IReadOnlyList<CoverageOptionDto> Options);

public class CompareCoverageValidator : Validator<CompareCoverageRequest>
{
    public CompareCoverageValidator()
    {
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.SumInsured).GreaterThan(0).LessThanOrEqualTo(50_000_000);
        RuleFor(x => x.NcbPercent).Must(PremiumCalculator.NcbSteps.Contains)
            .WithMessage("ส่วนลดประวัติดีต้องเป็น 0/20/30/40/50%");
        RuleFor(x => x.Deductible).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
/// POST /api/quotations/compare — rate the SAME vehicle/sum-insured across all coverage types
/// at once (side-by-side comparison for the sales conversation), without persisting anything.
/// </summary>
public class CompareCoverageEndpoint : Endpoint<CompareCoverageRequest, CompareCoverageResponse>
{
    /// <summary>Coverage types in descending order of cover (best → cheapest) for display.</summary>
    public static readonly IReadOnlyList<CoverageType> Order = new[]
    {
        CoverageType.Type1, CoverageType.Type2Plus, CoverageType.Type3Plus, CoverageType.Type3,
    };

    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public CompareCoverageEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Post("quotations/compare");
        Policies(PermissionPolicy.For(Perms.QuotationWrite));
    }

    public override async Task HandleAsync(CompareCoverageRequest r, CancellationToken ct)
    {
        var options = new List<CoverageOptionDto>(Order.Count);
        foreach (var coverage in Order)
        {
            var rating = await PremiumRatingService.RateAsync(
                _db, _clock.UtcNow.Year, r.VehicleId, coverage, r.SumInsured,
                r.NcbPercent, r.Deductible, r.RiderIds, ct);
            var b = rating.Breakdown;
            options.Add(new CoverageOptionDto(
                coverage.ToString(),
                new PremiumBreakdownDto(
                    b.BasePremium, b.VehicleAgeLoading, b.NcbDiscount,
                    b.DeductibleDiscount, b.RidersTotal, b.NetPremium)));
        }

        Response = new CompareCoverageResponse(options);
    }
}

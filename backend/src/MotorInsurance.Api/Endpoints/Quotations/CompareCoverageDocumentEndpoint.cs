using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Documents;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using QuestPDF.Fluent;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Quotations;

/// <summary>
/// POST /api/quotations/compare/document — the coverage comparison sheet (PDF) for the same
/// inputs as <see cref="CompareCoverageEndpoint"/>, suitable to hand to the customer.
/// </summary>
public class CompareCoverageDocumentEndpoint : Endpoint<CompareCoverageRequest>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public CompareCoverageDocumentEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Post("quotations/compare/document");
        Policies(PermissionPolicy.For(Perms.QuotationWrite));
    }

    public override async Task HandleAsync(CompareCoverageRequest r, CancellationToken ct)
    {
        var registration = await _db.Vehicles.AsNoTracking()
            .Where(v => v.Id == r.VehicleId)
            .Select(v => v.RegistrationNo)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Vehicle), r.VehicleId);

        var options = new List<CoverageComparisonOption>(CompareCoverageEndpoint.Order.Count);
        foreach (var coverage in CompareCoverageEndpoint.Order)
        {
            var b = (await PremiumRatingService.RateAsync(
                _db, _clock.UtcNow.Year, r.VehicleId, coverage, r.SumInsured,
                r.NcbPercent, r.Deductible, r.RiderIds, ct)).Breakdown;
            options.Add(new CoverageComparisonOption(
                ThaiLabels.Coverage(coverage), b.BasePremium, b.VehicleAgeLoading,
                b.NcbDiscount, b.DeductibleDiscount, b.RidersTotal, b.NetPremium));
        }

        var data = new CoverageComparisonData(
            registration, r.SumInsured, PremiumCalculator.NormalizeNcb(r.NcbPercent), r.Deductible,
            options, _clock.UtcNow);

        var pdf = new CoverageComparisonDocument(data).GeneratePdf();
        await Send.BytesAsync(pdf, $"compare-{registration}.pdf", contentType: "application/pdf", cancellation: ct);
    }
}

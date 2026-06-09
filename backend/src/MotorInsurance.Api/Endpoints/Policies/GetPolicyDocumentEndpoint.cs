using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Documents;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Entities;
using QuestPDF.Fluent;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

/// <summary>GET /api/policies/{id}/document — the policy schedule as a PDF (ตารางกรมธรรม์).</summary>
public class GetPolicyDocumentEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public GetPolicyDocumentEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("policies/{id}/document");
        Policies(PermissionPolicy.For(Perms.PolicyRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");

        var p = await _db.Policies.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.PolicyNo,
                x.Status,
                x.CoverageType,
                x.SumInsured,
                x.BasePremium,
                x.NcbPercent,
                x.Deductible,
                x.EffectiveDate,
                x.ExpiryDate,
                x.VehicleId,
                x.QuotationId,
                x.PreviousPolicyId,
                CustomerName = x.Customer.FullName,
                x.Customer.NationalId,
                x.Customer.Phone,
                x.Customer.Email,
                Reg = x.Vehicle.RegistrationNo,
                Prov = x.Vehicle.Province,
                Chassis = x.Vehicle.ChassisNo,
                Brand = x.Vehicle.ModelYear.Submodel.Model.Brand.Name,
                Model = x.Vehicle.ModelYear.Submodel.Model.Name,
                Submodel = x.Vehicle.ModelYear.Submodel.Name,
                Year = x.Vehicle.ModelYear.Year,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Policy), id);

        var riders = await _db.PolicyRiders.AsNoTracking()
            .Where(r => r.PolicyId == id).OrderBy(r => r.RiderId)
            .Select(r => new { r.RiderId, r.Rider.Name }).ToListAsync(ct);

        // Named drivers live on the originating quotation; a renewal climbs the PreviousPolicy chain.
        var quotationId = p.QuotationId ?? await ResolveQuotationIdAsync(p.PreviousPolicyId, ct);
        var drivers = quotationId is null
            ? new List<string>()
            : await _db.QuotationDrivers.AsNoTracking()
                .Where(d => d.QuotationId == quotationId).OrderBy(d => d.Id)
                .Select(d => d.FullName).ToListAsync(ct);

        // Re-derive the breakdown from the stored rating (vehicle age uses the effective year).
        var year = p.EffectiveDate?.Year ?? _clock.UtcNow.Year;
        var rating = await PremiumRatingService.RateAsync(
            _db, year, p.VehicleId, p.CoverageType, p.SumInsured,
            p.NcbPercent, p.Deductible, riders.Select(r => r.RiderId).ToList(), ct);

        var data = new PolicyScheduleData(
            p.PolicyNo, ThaiLabels.Coverage(p.CoverageType), ThaiLabels.PolicyStatus(p.Status),
            p.CustomerName, p.NationalId, p.Phone, p.Email,
            p.Reg, p.Prov, $"{p.Brand} {p.Model} {p.Submodel}", p.Year, p.Chassis,
            p.SumInsured, p.EffectiveDate, p.ExpiryDate, p.NcbPercent, p.Deductible,
            rating.Breakdown, riders.Select(r => r.Name).ToList(), drivers, _clock.UtcNow);

        var pdf = new PolicyScheduleDocument(data).GeneratePdf();
        await Send.BytesAsync(pdf, $"{p.PolicyNo}.pdf", contentType: "application/pdf", cancellation: ct);
    }

    private async Task<long?> ResolveQuotationIdAsync(long? previousId, CancellationToken ct)
    {
        while (previousId is not null)
        {
            var prev = await _db.Policies.AsNoTracking()
                .Where(p => p.Id == previousId)
                .Select(p => new { p.QuotationId, p.PreviousPolicyId })
                .FirstOrDefaultAsync(ct);
            if (prev is null) break;
            if (prev.QuotationId is not null) return prev.QuotationId;
            previousId = prev.PreviousPolicyId;
        }
        return null;
    }
}

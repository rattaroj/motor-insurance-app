using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Quotations;

public record DriverInput(string FullName, string NationalId, string IdCardImagePath);
public record CreateQuotationRequest(
    long CustomerId, long VehicleId, CoverageType CoverageType, decimal SumInsured,
    IReadOnlyList<DriverInput> Drivers,
    int NcbPercent = 0, decimal Deductible = 0, IReadOnlyList<long>? RiderIds = null);
public record CreateQuotationResponse(long Id);

public class CreateQuotationValidator : Validator<CreateQuotationRequest>
{
    public CreateQuotationValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.SumInsured).GreaterThan(0).LessThanOrEqualTo(50_000_000);
        RuleFor(x => x.NcbPercent).Must(PremiumCalculator.NcbSteps.Contains)
            .WithMessage("ส่วนลดประวัติดีต้องเป็น 0/20/30/40/50%");
        RuleFor(x => x.Deductible).GreaterThanOrEqualTo(0);
        // New-law requirement: 1–5 named drivers, each with an ID-card image.
        RuleFor(x => x.Drivers).NotEmpty().Must(d => d.Count <= 5)
            .WithMessage("ต้องระบุผู้ขับขี่ 1–5 คน");
        RuleForEach(x => x.Drivers).ChildRules(d =>
        {
            d.RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
            d.RuleFor(x => x.NationalId).NotEmpty().Length(13);
            d.RuleFor(x => x.IdCardImagePath).NotEmpty().MaximumLength(400);
        });
    }
}

/// <summary>POST /api/quotations — rate a quotation (PremiumCalculator) valid for 30 days.</summary>
public class CreateQuotationEndpoint : Endpoint<CreateQuotationRequest, CreateQuotationResponse>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public CreateQuotationEndpoint(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public override void Configure()
    {
        Post("quotations");
        Policies(PermissionPolicy.For(Perms.QuotationWrite));
    }

    public override async Task HandleAsync(CreateQuotationRequest r, CancellationToken ct)
    {
        if (await _db.Customers.FindAsync(new object[] { r.CustomerId }, ct) is null)
            throw new NotFoundException(nameof(Customer), r.CustomerId);

        var vehicle = await _db.Vehicles.FindAsync(new object[] { r.VehicleId }, ct)
            ?? throw new NotFoundException(nameof(Vehicle), r.VehicleId);
        if (vehicle.CustomerId != r.CustomerId)
            throw new ConflictException("Vehicle does not belong to the specified customer.");

        var rating = await PremiumRatingService.RateAsync(
            _db, _clock.UtcNow.Year, r.VehicleId, r.CoverageType, r.SumInsured,
            r.NcbPercent, r.Deductible, r.RiderIds, ct);

        var quotation = new Quotation
        {
            QuotationNo = await _docNo.NextAsync("QUO", ct),
            CustomerId = r.CustomerId,
            VehicleId = r.VehicleId,
            CoverageType = r.CoverageType,
            SumInsured = r.SumInsured,
            BasePremium = rating.Breakdown.BasePremium,
            Premium = rating.Breakdown.NetPremium,
            NcbPercent = PremiumCalculator.NormalizeNcb(r.NcbPercent),
            Deductible = r.Deductible,
            ValidUntil = DateOnly.FromDateTime(_clock.UtcNow.AddDays(30)),
            CreatedAt = _clock.UtcNow,
            Drivers = r.Drivers.Select(d => new QuotationDriver
            {
                FullName = d.FullName,
                NationalId = d.NationalId,
                IdCardImagePath = d.IdCardImagePath,
                CreatedAt = _clock.UtcNow,
            }).ToList(),
            Riders = rating.RiderIds.Select(id => new QuotationRider { RiderId = id }).ToList(),
        };
        _db.Quotations.Add(quotation);
        await _db.SaveChangesAsync(ct);

        await Send.ResponseAsync(new CreateQuotationResponse(quotation.Id), 201, ct);
    }
}

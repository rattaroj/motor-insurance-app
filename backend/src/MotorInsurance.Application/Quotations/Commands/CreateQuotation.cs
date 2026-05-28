using FluentValidation;
using MediatR;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Quotations.Commands;

public record CreateQuotationCommand(
    long CustomerId,
    long VehicleId,
    CoverageType CoverageType,
    decimal SumInsured) : IRequest<long>;

public class CreateQuotationValidator : AbstractValidator<CreateQuotationCommand>
{
    public CreateQuotationValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.VehicleId).GreaterThan(0);
        RuleFor(x => x.SumInsured).GreaterThan(0).LessThanOrEqualTo(50_000_000);
    }
}

public class CreateQuotationHandler : IRequestHandler<CreateQuotationCommand, long>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public CreateQuotationHandler(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public async Task<long> Handle(CreateQuotationCommand req, CancellationToken ct)
    {
        var customerExists = await _db.Customers.FindAsync(new object[] { req.CustomerId }, ct) is not null;
        if (!customerExists) throw new NotFoundException(nameof(Customer), req.CustomerId);

        var vehicle = await _db.Vehicles.FindAsync(new object[] { req.VehicleId }, ct)
            ?? throw new NotFoundException(nameof(Vehicle), req.VehicleId);
        if (vehicle.CustomerId != req.CustomerId)
            throw new ConflictException("Vehicle does not belong to the specified customer.");

        var quotation = new Quotation
        {
            QuotationNo = await _docNo.NextAsync("QUO", ct),
            CustomerId = req.CustomerId,
            VehicleId = req.VehicleId,
            CoverageType = req.CoverageType,
            SumInsured = req.SumInsured,
            Premium = PremiumCalculator.Calculate(req.CoverageType, req.SumInsured),
            ValidUntil = DateOnly.FromDateTime(_clock.UtcNow.AddDays(30)),
            CreatedAt = _clock.UtcNow,
        };

        _db.Quotations.Add(quotation);
        await _db.SaveChangesAsync(ct);
        return quotation.Id;
    }
}

/// <summary>Simplified premium rating. Real systems plug in an actuarial engine here.</summary>
public static class PremiumCalculator
{
    public static decimal Calculate(CoverageType coverage, decimal sumInsured)
    {
        var rate = coverage switch
        {
            CoverageType.Type1     => 0.045m,
            CoverageType.Type2Plus => 0.030m,
            CoverageType.Type3Plus => 0.022m,
            CoverageType.Type3     => 0.015m,
            _ => 0.045m
        };
        return Math.Round(sumInsured * rate, 2);
    }
}

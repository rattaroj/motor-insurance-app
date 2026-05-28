using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;

namespace MotorInsurance.Application.Quotations.Queries;

public record QuotationDto(
    long Id, string QuotationNo, long CustomerId, string CustomerName,
    long VehicleId, string VehicleRegistration, string CoverageType,
    decimal SumInsured, decimal Premium, DateOnly ValidUntil);

public record GetQuotationsQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<PagedResult<QuotationDto>>;

public class GetQuotationsHandler : IRequestHandler<GetQuotationsQuery, PagedResult<QuotationDto>>
{
    private readonly IAppDbContext _db;
    public GetQuotationsHandler(IAppDbContext db) => _db = db;

    public async Task<PagedResult<QuotationDto>> Handle(GetQuotationsQuery req, CancellationToken ct)
    {
        var query = _db.Quotations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            query = query.Where(q =>
                q.QuotationNo.Contains(s) ||
                q.Customer.FullName.Contains(s) ||
                q.Vehicle.RegistrationNo.Contains(s));
        }

        // CoverageType is a converted enum, so map ToString() in memory.
        return await query
            .OrderByDescending(q => q.Id)
            .Select(q => new
            {
                q.Id, q.QuotationNo, q.CustomerId,
                CustomerName = q.Customer.FullName,
                q.VehicleId,
                VehicleRegistration = q.Vehicle.RegistrationNo,
                q.CoverageType, q.SumInsured, q.Premium, q.ValidUntil,
            })
            .ToPagedResultAsync(
                req.Page, req.PageSize,
                r => new QuotationDto(
                    r.Id, r.QuotationNo, r.CustomerId, r.CustomerName,
                    r.VehicleId, r.VehicleRegistration, r.CoverageType.ToString(),
                    r.SumInsured, r.Premium, r.ValidUntil),
                ct);
    }
}

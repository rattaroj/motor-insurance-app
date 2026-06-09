using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Quotations;

public record QuotationDto(
    long Id, string QuotationNo, long CustomerId, string CustomerName,
    long VehicleId, string VehicleRegistration, string CoverageType,
    decimal SumInsured, decimal Premium, DateOnly ValidUntil,
    decimal BasePremium, int NcbPercent, decimal Deductible, IReadOnlyList<string> Riders);

public class ListQuotationsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

/// <summary>GET /api/quotations — paged, free-text search.</summary>
public class ListQuotationsEndpoint : Endpoint<ListQuotationsRequest, PagedResult<QuotationDto>>
{
    private readonly IAppDbContext _db;
    public ListQuotationsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("quotations");
        Policies(PermissionPolicy.For(Perms.QuotationRead));
    }

    public override async Task HandleAsync(ListQuotationsRequest r, CancellationToken ct)
    {
        var query = _db.Quotations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim();
            query = query.Where(q =>
                q.QuotationNo.Contains(s) ||
                q.Customer.FullName.Contains(s) ||
                q.Vehicle.RegistrationNo.Contains(s));
        }

        // CoverageType is a converted enum, so map ToString() in memory.
        Response = await query
            .OrderByDescending(q => q.Id)
            .Select(q => new
            {
                q.Id,
                q.QuotationNo,
                q.CustomerId,
                CustomerName = q.Customer.FullName,
                q.VehicleId,
                VehicleRegistration = q.Vehicle.RegistrationNo,
                q.CoverageType,
                q.SumInsured,
                q.Premium,
                q.ValidUntil,
                q.BasePremium,
                q.NcbPercent,
                q.Deductible,
                Riders = q.Riders.Select(x => x.Rider.Name).ToList(),
            })
            .ToPagedResultAsync(
                r.Page, r.PageSize,
                x => new QuotationDto(
                    x.Id, x.QuotationNo, x.CustomerId, x.CustomerName,
                    x.VehicleId, x.VehicleRegistration, x.CoverageType.ToString(),
                    x.SumInsured, x.Premium, x.ValidUntil,
                    x.BasePremium, x.NcbPercent, x.Deductible, x.Riders),
                ct);
    }
}

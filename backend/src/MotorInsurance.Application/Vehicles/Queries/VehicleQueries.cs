using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;

namespace MotorInsurance.Application.Vehicles.Queries;

public record VehicleDto(
    long Id, long CustomerId, string CustomerName, string RegistrationNo, string Province,
    long ModelYearId, string Brand, string Model, string Submodel, int Year, string? ChassisNo);

public record GetVehiclesQuery(int Page = 1, int PageSize = 20, string? Search = null, long? CustomerId = null)
    : IRequest<PagedResult<VehicleDto>>;

public class GetVehiclesHandler : IRequestHandler<GetVehiclesQuery, PagedResult<VehicleDto>>
{
    private readonly IAppDbContext _db;
    public GetVehiclesHandler(IAppDbContext db) => _db = db;

    public async Task<PagedResult<VehicleDto>> Handle(GetVehiclesQuery req, CancellationToken ct)
    {
        var query = _db.Vehicles.AsNoTracking().AsQueryable();

        if (req.CustomerId is { } cid)
            query = query.Where(v => v.CustomerId == cid);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            query = query.Where(v =>
                v.RegistrationNo.Contains(s) ||
                v.Province.Contains(s) ||
                v.ModelYear.Submodel.Model.Brand.Name.Contains(s) ||
                v.ModelYear.Submodel.Model.Name.Contains(s) ||
                v.ModelYear.Submodel.Name.Contains(s) ||
                v.Customer.FullName.Contains(s));
        }

        return await query
            .OrderByDescending(v => v.Id)
            .Select(v => new VehicleDto(
                v.Id,
                v.CustomerId,
                v.Customer.FullName,
                v.RegistrationNo,
                v.Province,
                v.ModelYearId,
                v.ModelYear.Submodel.Model.Brand.Name,
                v.ModelYear.Submodel.Model.Name,
                v.ModelYear.Submodel.Name,
                v.ModelYear.Year,
                v.ChassisNo))
            .ToPagedResultAsync(req.Page, req.PageSize, ct);
    }
}

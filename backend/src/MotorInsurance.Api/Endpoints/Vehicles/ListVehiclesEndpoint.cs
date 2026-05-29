using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Vehicles;

public record VehicleDto(
    long Id, long CustomerId, string CustomerName, string RegistrationNo, string Province,
    long ModelYearId, string Brand, string Model, string Submodel, Powertrain Powertrain,
    int Year, string? ChassisNo);

public class ListVehiclesRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public long? CustomerId { get; set; }
}

/// <summary>GET /api/vehicles — paged, filterable by owner + free-text search.</summary>
public class ListVehiclesEndpoint : Endpoint<ListVehiclesRequest, PagedResult<VehicleDto>>
{
    private readonly IAppDbContext _db;
    public ListVehiclesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("vehicles");
        Policies(PermissionPolicy.For(Perms.VehicleRead));
    }

    public override async Task HandleAsync(ListVehiclesRequest r, CancellationToken ct)
    {
        var query = _db.Vehicles.AsNoTracking().AsQueryable();

        if (r.CustomerId is { } cid)
            query = query.Where(v => v.CustomerId == cid);

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim();
            query = query.Where(v =>
                v.RegistrationNo.Contains(s) ||
                v.Province.Contains(s) ||
                v.ModelYear.Submodel.Model.Brand.Name.Contains(s) ||
                v.ModelYear.Submodel.Model.Name.Contains(s) ||
                v.ModelYear.Submodel.Name.Contains(s) ||
                v.Customer.FullName.Contains(s));
        }

        Response = await query
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
                v.ModelYear.Submodel.Powertrain,
                v.ModelYear.Year,
                v.ChassisNo))
            .ToPagedResultAsync(r.Page, r.PageSize, ct);
    }
}

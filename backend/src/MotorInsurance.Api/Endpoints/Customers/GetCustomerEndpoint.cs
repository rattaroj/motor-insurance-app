using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Customers;

/// <summary>GET /api/customers/{id} — single customer incl. resolved address names.</summary>
public class GetCustomerEndpoint : EndpointWithoutRequest<CustomerDto>
{
    private readonly IAppDbContext _db;
    public GetCustomerEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("customers/{id}");
        Policies(PermissionPolicy.For(Perms.CustomerRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        Response = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerDto(
                c.Id, c.NationalId,
                c.Title, c.FirstName, c.LastName, c.FullName, c.BirthDate,
                c.Phone, c.Email,
                c.AddressLine,
                c.ProvinceId, c.Province != null ? c.Province.NameTh : null,
                c.DistrictId, c.District != null ? c.District.NameTh : null,
                c.SubdistrictId, c.Subdistrict != null ? c.Subdistrict.NameTh : null,
                c.PostalCodeId, c.PostalCode != null ? c.PostalCode.Code : null))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Customer), id);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Customers;

public record CustomerDto(long Id, string NationalId, string FullName, string? Phone, string? Email);

public class ListCustomersRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
}

/// <summary>GET /api/customers — paged, free-text search over id/name/phone/email.</summary>
public class ListCustomersEndpoint : Endpoint<ListCustomersRequest, PagedResult<CustomerDto>>
{
    private readonly IAppDbContext _db;
    public ListCustomersEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("customers");
        Policies(PermissionPolicy.For(Perms.CustomerRead));
    }

    public override async Task HandleAsync(ListCustomersRequest r, CancellationToken ct)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim();
            query = query.Where(c =>
                c.NationalId.Contains(s) ||
                c.FullName.Contains(s) ||
                (c.Phone != null && c.Phone.Contains(s)) ||
                (c.Email != null && c.Email.Contains(s)));
        }

        Response = await query
            .OrderByDescending(c => c.Id)
            .Select(c => new CustomerDto(c.Id, c.NationalId, c.FullName, c.Phone, c.Email))
            .ToPagedResultAsync(r.Page, r.PageSize, ct);
    }
}

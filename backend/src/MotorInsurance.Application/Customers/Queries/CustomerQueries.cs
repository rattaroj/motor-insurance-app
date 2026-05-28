using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;

namespace MotorInsurance.Application.Customers.Queries;

public record CustomerDto(long Id, string NationalId, string FullName, string? Phone, string? Email);

public record GetCustomersQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<PagedResult<CustomerDto>>;

public class GetCustomersHandler : IRequestHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    private readonly IAppDbContext _db;
    public GetCustomersHandler(IAppDbContext db) => _db = db;

    public async Task<PagedResult<CustomerDto>> Handle(GetCustomersQuery req, CancellationToken ct)
    {
        var query = _db.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            query = query.Where(c =>
                c.NationalId.Contains(s) ||
                c.FullName.Contains(s) ||
                (c.Phone != null && c.Phone.Contains(s)) ||
                (c.Email != null && c.Email.Contains(s)));
        }

        return await query
            .OrderByDescending(c => c.Id)
            .Select(c => new CustomerDto(c.Id, c.NationalId, c.FullName, c.Phone, c.Email))
            .ToPagedResultAsync(req.Page, req.PageSize, ct);
    }
}

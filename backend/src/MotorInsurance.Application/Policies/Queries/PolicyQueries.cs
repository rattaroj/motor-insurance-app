using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Policies.Queries;

// ---- List policies (paged, filterable by status + search) ----
public record GetPoliciesQuery(int Page = 1, int PageSize = 20, string? Status = null, string? Search = null)
    : IRequest<PagedResult<PolicyDto>>;

public class GetPoliciesHandler : IRequestHandler<GetPoliciesQuery, PagedResult<PolicyDto>>
{
    private readonly IAppDbContext _db;
    public GetPoliciesHandler(IAppDbContext db) => _db = db;

    public async Task<PagedResult<PolicyDto>> Handle(GetPoliciesQuery req, CancellationToken ct)
    {
        var query = _db.Policies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.Status)
            && Enum.TryParse<Domain.Enums.PolicyStatus>(req.Status, true, out var s))
            query = query.Where(p => p.Status == s);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var search = req.Search.Trim();
            query = query.Where(p =>
                p.PolicyNo.Contains(search) ||
                p.Customer.FullName.Contains(search) ||
                p.Vehicle.RegistrationNo.Contains(search));
        }

        // Status/CoverageType are converted enums, so map ToString() in memory.
        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id, p.PolicyNo, p.CustomerId,
                CustomerName = p.Customer.FullName,
                p.VehicleId,
                VehicleRegistration = p.Vehicle.RegistrationNo,
                p.Status, p.CoverageType, p.SumInsured, p.Premium,
                p.EffectiveDate, p.ExpiryDate, p.PreviousPolicyId,
            })
            .ToPagedResultAsync(
                req.Page, req.PageSize,
                r => new PolicyDto(
                    r.Id, r.PolicyNo, r.CustomerId, r.CustomerName,
                    r.VehicleId, r.VehicleRegistration, r.Status.ToString(), r.CoverageType.ToString(),
                    r.SumInsured, r.Premium, r.EffectiveDate, r.ExpiryDate, r.PreviousPolicyId),
                ct);
    }

    internal static PolicyDto MapToDto(Policy p) => new(
        p.Id, p.PolicyNo, p.CustomerId, p.Customer.FullName,
        p.VehicleId, p.Vehicle.RegistrationNo,
        p.Status.ToString(), p.CoverageType.ToString(),
        p.SumInsured, p.Premium, p.EffectiveDate, p.ExpiryDate, p.PreviousPolicyId);
}

// ---- Get single policy ----
public record GetPolicyByIdQuery(long Id) : IRequest<PolicyDto>;

public class GetPolicyByIdHandler : IRequestHandler<GetPolicyByIdQuery, PolicyDto>
{
    private readonly IAppDbContext _db;
    public GetPolicyByIdHandler(IAppDbContext db) => _db = db;

    public async Task<PolicyDto> Handle(GetPolicyByIdQuery req, CancellationToken ct)
    {
        var p = await _db.Policies
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new NotFoundException(nameof(Policy), req.Id);
        return GetPoliciesHandler.MapToDto(p);
    }
}

// ---- Temporal history (SQL Server system-versioned) ----
// The actual TemporalAll() query lives in Infrastructure (IPolicyHistoryReader)
// because it is a SQL Server provider API; this handler just delegates.
public record GetPolicyHistoryQuery(long Id) : IRequest<IReadOnlyList<PolicyHistoryDto>>;

public class GetPolicyHistoryHandler
    : IRequestHandler<GetPolicyHistoryQuery, IReadOnlyList<PolicyHistoryDto>>
{
    private readonly IPolicyHistoryReader _history;
    public GetPolicyHistoryHandler(IPolicyHistoryReader history) => _history = history;

    public Task<IReadOnlyList<PolicyHistoryDto>> Handle(GetPolicyHistoryQuery req, CancellationToken ct)
        => _history.GetHistoryAsync(req.Id, ct);
}

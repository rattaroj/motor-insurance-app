using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

public class ListPoliciesRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public string? Search { get; set; }
}

/// <summary>GET /api/policies — paged, filterable by status + free-text search.</summary>
public class ListPoliciesEndpoint : Endpoint<ListPoliciesRequest, PagedResult<PolicyDto>>
{
    private readonly IAppDbContext _db;
    public ListPoliciesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("policies");
        Policies(PermissionPolicy.For(Perms.PolicyRead));
    }

    public override async Task HandleAsync(ListPoliciesRequest r, CancellationToken ct)
    {
        var query = _db.Policies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(r.Status) && Enum.TryParse<PolicyStatus>(r.Status, true, out var s))
            query = query.Where(p => p.Status == s);

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var search = r.Search.Trim();
            query = query.Where(p =>
                p.PolicyNo.Contains(search) ||
                p.Customer.FullName.Contains(search) ||
                p.Vehicle.RegistrationNo.Contains(search));
        }

        // Status/CoverageType are converted enums, so map ToString() in memory.
        Response = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                p.PolicyNo,
                p.CustomerId,
                CustomerName = p.Customer.FullName,
                p.VehicleId,
                VehicleRegistration = p.Vehicle.RegistrationNo,
                p.Status,
                p.CoverageType,
                p.SumInsured,
                p.Premium,
                p.BasePremium,
                p.NcbPercent,
                p.Deductible,
                p.EffectiveDate,
                p.ExpiryDate,
                p.PreviousPolicyId,
            })
            .ToPagedResultAsync(
                r.Page, r.PageSize,
                x => new PolicyDto(
                    x.Id, x.PolicyNo, x.CustomerId, x.CustomerName,
                    x.VehicleId, x.VehicleRegistration, x.Status.ToString(), x.CoverageType.ToString(),
                    x.SumInsured, x.Premium, x.BasePremium, x.NcbPercent, x.Deductible,
                    x.EffectiveDate, x.ExpiryDate, x.PreviousPolicyId),
                ct);
    }
}

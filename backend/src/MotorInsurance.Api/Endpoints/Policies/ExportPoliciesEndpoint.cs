using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Common;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

/// <summary>
/// GET /api/policies/export?status=&amp;search= — the policy list as a CSV download.
/// Same status/free-text filter as <see cref="ListPoliciesEndpoint"/>, but every matching row
/// (no paging) so finance can pull the full book into Excel.
/// </summary>
public class ExportPoliciesEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public ExportPoliciesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("policies/export");
        Policies(PermissionPolicy.For(Perms.PolicyRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var statusText = Query<string?>("status", isRequired: false);
        var search = Query<string?>("search", isRequired: false);

        var query = _db.Policies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(statusText) && Enum.TryParse<PolicyStatus>(statusText, true, out var s))
            query = query.Where(p => p.Status == s);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.PolicyNo.Contains(term) ||
                p.Customer.FullName.Contains(term) ||
                p.Vehicle.RegistrationNo.Contains(term));
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.PolicyNo,
                CustomerName = p.Customer.FullName,
                Registration = p.Vehicle.RegistrationNo,
                p.Status,
                p.CoverageType,
                p.SumInsured,
                p.Premium,
                p.NcbPercent,
                p.EffectiveDate,
                p.ExpiryDate,
            })
            .ToListAsync(ct);

        var csv = Csv.Build(
            new[] { "PolicyNo", "Customer", "Registration", "Status", "Coverage", "SumInsured", "Premium", "NCB%", "EffectiveDate", "ExpiryDate" },
            rows.Select(r => new[]
            {
                r.PolicyNo, r.CustomerName, r.Registration, r.Status.ToString(), r.CoverageType.ToString(),
                Csv.Num(r.SumInsured), Csv.Num(r.Premium), r.NcbPercent.ToString(),
                Csv.Date(r.EffectiveDate), Csv.Date(r.ExpiryDate),
            }));

        await Send.BytesAsync(csv, "policies.csv", contentType: "text/csv", cancellation: ct);
    }
}

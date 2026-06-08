using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Common;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

/// <summary>
/// GET /api/claims/export?status=&amp;search=&amp;policyId= — the claim list as a CSV download.
/// Same filter as <see cref="ListClaimsEndpoint"/>, all matching rows (no paging).
/// </summary>
public class ExportClaimsEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public ExportClaimsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("claims/export");
        Policies(PermissionPolicy.For(Perms.ClaimRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var statusText = Query<string?>("status", isRequired: false);
        var search = Query<string?>("search", isRequired: false);
        var policyId = Query<long?>("policyId", isRequired: false);

        var query = _db.Claims.AsNoTracking().AsQueryable();
        if (policyId is { } pid)
            query = query.Where(c => c.PolicyId == pid);
        if (!string.IsNullOrWhiteSpace(statusText) && Enum.TryParse<ClaimStatus>(statusText, true, out var status))
            query = query.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.ClaimNo.Contains(term) || c.Policy.PolicyNo.Contains(term));
        }

        var rows = await query
            .OrderByDescending(c => c.Id)
            .Select(c => new
            {
                c.ClaimNo, PolicyNo = c.Policy.PolicyNo, c.Status, c.IncidentDate,
                c.ClaimedAmount, c.ApprovedAmount,
                GarageName = c.Garage != null ? c.Garage.Name : null, c.SurveyorName,
            })
            .ToListAsync(ct);

        var csv = Csv.Build(
            new[] { "ClaimNo", "PolicyNo", "Status", "IncidentDate", "ClaimedAmount", "ApprovedAmount", "Garage", "Surveyor" },
            rows.Select(r => new[]
            {
                r.ClaimNo, r.PolicyNo, r.Status.ToString(), Csv.Date(r.IncidentDate),
                Csv.Num(r.ClaimedAmount), Csv.Num(r.ApprovedAmount), r.GarageName, r.SurveyorName,
            }));

        await Send.BytesAsync(csv, "claims.csv", contentType: "text/csv", cancellation: ct);
    }
}

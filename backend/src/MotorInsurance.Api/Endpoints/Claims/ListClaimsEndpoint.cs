using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public record ClaimDto(
    long Id, string ClaimNo, long PolicyId, string PolicyNo, string Status,
    DateOnly IncidentDate, string? Description, decimal ClaimedAmount,
    decimal? ApprovedAmount, string? RejectReason);

public class ListClaimsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Status { get; set; }
    public long? PolicyId { get; set; }
}

/// <summary>GET /api/claims — paged, filterable by status / policy / free-text search.</summary>
public class ListClaimsEndpoint : Endpoint<ListClaimsRequest, PagedResult<ClaimDto>>
{
    private readonly IAppDbContext _db;
    public ListClaimsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("claims");
        Policies(PermissionPolicy.For(Perms.ClaimRead));
    }

    public override async Task HandleAsync(ListClaimsRequest r, CancellationToken ct)
    {
        var query = _db.Claims.AsNoTracking().AsQueryable();

        if (r.PolicyId is { } pid)
            query = query.Where(c => c.PolicyId == pid);

        if (!string.IsNullOrWhiteSpace(r.Status) && Enum.TryParse<ClaimStatus>(r.Status, true, out var status))
            query = query.Where(c => c.Status == status);

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim();
            query = query.Where(c => c.ClaimNo.Contains(s) || c.Policy.PolicyNo.Contains(s));
        }

        // Status is a converted enum, so map ToString() in memory.
        Response = await query
            .OrderByDescending(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.ClaimNo,
                c.PolicyId,
                PolicyNo = c.Policy.PolicyNo,
                c.Status,
                c.IncidentDate,
                c.Description,
                c.ClaimedAmount,
                c.ApprovedAmount,
                c.RejectReason,
            })
            .ToPagedResultAsync(
                r.Page, r.PageSize,
                x => new ClaimDto(
                    x.Id, x.ClaimNo, x.PolicyId, x.PolicyNo, x.Status.ToString(),
                    x.IncidentDate, x.Description, x.ClaimedAmount, x.ApprovedAmount, x.RejectReason),
                ct);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Claims.Queries;

public record ClaimDto(
    long Id, string ClaimNo, long PolicyId, string PolicyNo, string Status,
    DateOnly IncidentDate, string? Description, decimal ClaimedAmount,
    decimal? ApprovedAmount, string? RejectReason);

public record GetClaimsQuery(
    int Page = 1, int PageSize = 20, string? Search = null, string? Status = null, long? PolicyId = null)
    : IRequest<PagedResult<ClaimDto>>;

public class GetClaimsHandler : IRequestHandler<GetClaimsQuery, PagedResult<ClaimDto>>
{
    private readonly IAppDbContext _db;
    public GetClaimsHandler(IAppDbContext db) => _db = db;

    public async Task<PagedResult<ClaimDto>> Handle(GetClaimsQuery req, CancellationToken ct)
    {
        var query = _db.Claims.AsNoTracking().AsQueryable();

        if (req.PolicyId is { } pid)
            query = query.Where(c => c.PolicyId == pid);

        if (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<ClaimStatus>(req.Status, true, out var status))
            query = query.Where(c => c.Status == status);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            query = query.Where(c => c.ClaimNo.Contains(s) || c.Policy.PolicyNo.Contains(s));
        }

        // Status is a converted enum, so map ToString() in memory.
        return await query
            .OrderByDescending(c => c.Id)
            .Select(c => new
            {
                c.Id, c.ClaimNo, c.PolicyId,
                PolicyNo = c.Policy.PolicyNo,
                c.Status, c.IncidentDate, c.Description,
                c.ClaimedAmount, c.ApprovedAmount, c.RejectReason,
            })
            .ToPagedResultAsync(
                req.Page, req.PageSize,
                r => new ClaimDto(
                    r.Id, r.ClaimNo, r.PolicyId, r.PolicyNo, r.Status.ToString(),
                    r.IncidentDate, r.Description, r.ClaimedAmount, r.ApprovedAmount, r.RejectReason),
                ct);
    }
}

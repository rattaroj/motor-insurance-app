using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Payments.Queries;

public record PaymentDto(
    long Id, string PaymentNo, string Direction, string Status,
    long? PolicyId, string? PolicyNo, long? ClaimId, string? ClaimNo,
    decimal Amount, DateTime? PaidAt, string? ReferenceNo);

public record GetPaymentsQuery(
    int Page = 1, int PageSize = 20, string? Search = null, string? Status = null,
    string? Direction = null, long? PolicyId = null, long? ClaimId = null)
    : IRequest<PagedResult<PaymentDto>>;

public class GetPaymentsHandler : IRequestHandler<GetPaymentsQuery, PagedResult<PaymentDto>>
{
    private readonly IAppDbContext _db;
    public GetPaymentsHandler(IAppDbContext db) => _db = db;

    public async Task<PagedResult<PaymentDto>> Handle(GetPaymentsQuery req, CancellationToken ct)
    {
        var query = _db.Payments.AsNoTracking().AsQueryable();

        if (req.PolicyId is { } pid)
            query = query.Where(p => p.PolicyId == pid);
        if (req.ClaimId is { } cid)
            query = query.Where(p => p.ClaimId == cid);
        if (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<PaymentStatus>(req.Status, true, out var status))
            query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(req.Direction) && Enum.TryParse<PaymentDirection>(req.Direction, true, out var dir))
            query = query.Where(p => p.Direction == dir);

        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            query = query.Where(p =>
                p.PaymentNo.Contains(s) ||
                (p.Policy != null && p.Policy.PolicyNo.Contains(s)) ||
                (p.Claim != null && p.Claim.ClaimNo.Contains(s)));
        }

        // Direction and Status are converted enums, so map ToString() in memory.
        return await query
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id, p.PaymentNo, p.Direction, p.Status,
                p.PolicyId, PolicyNo = p.Policy != null ? p.Policy.PolicyNo : null,
                p.ClaimId, ClaimNo = p.Claim != null ? p.Claim.ClaimNo : null,
                p.Amount, p.PaidAt, p.ReferenceNo,
            })
            .ToPagedResultAsync(
                req.Page, req.PageSize,
                r => new PaymentDto(
                    r.Id, r.PaymentNo, r.Direction.ToString(), r.Status.ToString(),
                    r.PolicyId, r.PolicyNo, r.ClaimId, r.ClaimNo,
                    r.Amount, r.PaidAt, r.ReferenceNo),
                ct);
    }
}

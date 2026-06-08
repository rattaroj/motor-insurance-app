using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Payments;

public record PaymentDto(
    long Id, string PaymentNo, string Direction, string Status,
    long? PolicyId, string? PolicyNo, long? ClaimId, string? ClaimNo,
    decimal Amount, DateTime? PaidAt, string? ReferenceNo,
    int? InstallmentSeq, DateOnly? DueDate);

public class ListPaymentsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Direction { get; set; }
    public long? PolicyId { get; set; }
    public long? ClaimId { get; set; }
}

/// <summary>GET /api/payments — paged, filterable by status/direction/policy/claim + search.</summary>
public class ListPaymentsEndpoint : Endpoint<ListPaymentsRequest, PagedResult<PaymentDto>>
{
    private readonly IAppDbContext _db;
    public ListPaymentsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("payments");
        Policies(PermissionPolicy.For(Perms.PaymentRead));
    }

    public override async Task HandleAsync(ListPaymentsRequest r, CancellationToken ct)
    {
        var query = _db.Payments.AsNoTracking().AsQueryable();

        if (r.PolicyId is { } pid)
            query = query.Where(p => p.PolicyId == pid);
        if (r.ClaimId is { } cid)
            query = query.Where(p => p.ClaimId == cid);
        if (!string.IsNullOrWhiteSpace(r.Status) && Enum.TryParse<PaymentStatus>(r.Status, true, out var status))
            query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(r.Direction) && Enum.TryParse<PaymentDirection>(r.Direction, true, out var dir))
            query = query.Where(p => p.Direction == dir);

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim();
            query = query.Where(p =>
                p.PaymentNo.Contains(s) ||
                (p.Policy != null && p.Policy.PolicyNo.Contains(s)) ||
                (p.Claim != null && p.Claim.ClaimNo.Contains(s)));
        }

        // Direction and Status are converted enums, so map ToString() in memory.
        Response = await query
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id, p.PaymentNo, p.Direction, p.Status,
                p.PolicyId, PolicyNo = p.Policy != null ? p.Policy.PolicyNo : null,
                p.ClaimId, ClaimNo = p.Claim != null ? p.Claim.ClaimNo : null,
                p.Amount, p.PaidAt, p.ReferenceNo, p.InstallmentSeq, p.DueDate,
            })
            .ToPagedResultAsync(
                r.Page, r.PageSize,
                x => new PaymentDto(
                    x.Id, x.PaymentNo, x.Direction.ToString(), x.Status.ToString(),
                    x.PolicyId, x.PolicyNo, x.ClaimId, x.ClaimNo,
                    x.Amount, x.PaidAt, x.ReferenceNo, x.InstallmentSeq, x.DueDate),
                ct);
    }
}

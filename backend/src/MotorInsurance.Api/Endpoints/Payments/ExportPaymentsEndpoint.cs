using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Common;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Payments;

/// <summary>
/// GET /api/payments/export?status=&amp;direction=&amp;search=&amp;policyId=&amp;claimId= — the payment
/// ledger as a CSV download. Same filter as <see cref="ListPaymentsEndpoint"/>, all matching rows.
/// </summary>
public class ExportPaymentsEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public ExportPaymentsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("payments/export");
        Policies(PermissionPolicy.For(Perms.PaymentRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var statusText = Query<string?>("status", isRequired: false);
        var directionText = Query<string?>("direction", isRequired: false);
        var search = Query<string?>("search", isRequired: false);
        var policyId = Query<long?>("policyId", isRequired: false);
        var claimId = Query<long?>("claimId", isRequired: false);

        var query = _db.Payments.AsNoTracking().AsQueryable();
        if (policyId is { } pid)
            query = query.Where(p => p.PolicyId == pid);
        if (claimId is { } cid)
            query = query.Where(p => p.ClaimId == cid);
        if (!string.IsNullOrWhiteSpace(statusText) && Enum.TryParse<PaymentStatus>(statusText, true, out var status))
            query = query.Where(p => p.Status == status);
        if (!string.IsNullOrWhiteSpace(directionText) && Enum.TryParse<PaymentDirection>(directionText, true, out var dir))
            query = query.Where(p => p.Direction == dir);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.PaymentNo.Contains(term) ||
                (p.Policy != null && p.Policy.PolicyNo.Contains(term)) ||
                (p.Claim != null && p.Claim.ClaimNo.Contains(term)));
        }

        var rows = await query
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.PaymentNo,
                p.Direction,
                p.Status,
                PolicyNo = p.Policy != null ? p.Policy.PolicyNo : null,
                ClaimNo = p.Claim != null ? p.Claim.ClaimNo : null,
                p.Amount,
                p.PaidAt,
                p.ReferenceNo,
            })
            .ToListAsync(ct);

        var csv = Csv.Build(
            new[] { "PaymentNo", "Direction", "Status", "PolicyNo", "ClaimNo", "Amount", "PaidAt", "ReferenceNo" },
            rows.Select(r => new[]
            {
                r.PaymentNo, r.Direction.ToString(), r.Status.ToString(), r.PolicyNo, r.ClaimNo,
                Csv.Num(r.Amount), Csv.Date(r.PaidAt), r.ReferenceNo,
            }));

        await Send.BytesAsync(csv, "payments.csv", contentType: "text/csv", cancellation: ct);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Common;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Policies;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Renewals;

/// <summary>
/// GET /api/renewals/expiring/export?days=60 — the renewal worklist as a CSV download
/// (same set as <see cref="GetExpiringPoliciesEndpoint"/>).
/// </summary>
public class ExportExpiringEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public ExportExpiringEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("renewals/expiring/export");
        Policies(PermissionPolicy.For(Perms.PolicyRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var days = Query<int?>("days", isRequired: false) ?? PolicyQueries.RenewalWindowDays;
        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);

        var rows = await _db.Policies.AsNoTracking()
            .ExpiringWithin(today, days)
            .NotYetRenewed(_db)
            .OrderBy(p => p.ExpiryDate)
            .Select(p => new
            {
                p.PolicyNo,
                CustomerName = p.Customer.FullName,
                p.Customer.Email,
                p.Customer.Phone,
                p.ExpiryDate,
                LastRemindedAt = _db.Notifications.Where(n => n.PolicyId == p.Id).Max(n => (DateTime?)n.SentAt),
            })
            .ToListAsync(ct);

        var csv = Csv.Build(
            new[] { "PolicyNo", "Customer", "Email", "Phone", "ExpiryDate", "DaysLeft", "LastRemindedAt" },
            rows.Select(r => new[]
            {
                r.PolicyNo, r.CustomerName, r.Email, r.Phone, Csv.Date(r.ExpiryDate),
                r.ExpiryDate is null ? "" : (r.ExpiryDate.Value.DayNumber - today.DayNumber).ToString(),
                Csv.Date(r.LastRemindedAt),
            }));

        await Send.BytesAsync(csv, "renewals-expiring.csv", contentType: "text/csv", cancellation: ct);
    }
}

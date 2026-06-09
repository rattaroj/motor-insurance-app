using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Policies;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Renewals;

public record ExpiringPolicyDto(
    long PolicyId, string PolicyNo, string CustomerName, string? CustomerEmail, string? CustomerPhone,
    DateOnly ExpiryDate, int DaysLeft, DateTime? LastRemindedAt);

/// <summary>
/// GET /api/renewals/expiring?days=60 — Active policies expiring within N days that have not
/// yet been renewed (the proactive renewal worklist).
/// </summary>
public class GetExpiringPoliciesEndpoint : EndpointWithoutRequest<IReadOnlyList<ExpiringPolicyDto>>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public GetExpiringPoliciesEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("renewals/expiring");
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
                p.Id,
                p.PolicyNo,
                CustomerName = p.Customer.FullName,
                p.Customer.Email,
                p.Customer.Phone,
                p.ExpiryDate,
                LastRemindedAt = _db.Notifications.Where(n => n.PolicyId == p.Id).Max(n => (DateTime?)n.SentAt),
            })
            .ToListAsync(ct);

        Response = rows.Select(r => new ExpiringPolicyDto(
            r.Id, r.PolicyNo, r.CustomerName, r.Email, r.Phone, r.ExpiryDate!.Value,
            r.ExpiryDate!.Value.DayNumber - today.DayNumber, r.LastRemindedAt)).ToList();
    }
}

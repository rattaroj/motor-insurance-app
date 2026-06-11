using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Payments;

public record OverdueInstallmentDto(
    long PaymentId, string PaymentNo, long PolicyId, string PolicyNo,
    string CustomerName, string? CustomerEmail, string? CustomerPhone,
    int InstallmentSeq, decimal Amount, DateOnly DueDate, int DaysOverdue, DateTime? LastRemindedAt);

/// <summary>
/// GET /api/payments/overdue — inbound premium installments still Pending past their due date
/// (the dunning worklist). Mirrors the renewals "expiring" worklist.
/// </summary>
public class GetOverdueInstallmentsEndpoint : EndpointWithoutRequest<IReadOnlyList<OverdueInstallmentDto>>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public GetOverdueInstallmentsEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("payments/overdue");
        Policies(PermissionPolicy.For(Perms.PaymentRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);

        var rows = await _db.Payments.AsNoTracking()
            .Where(p => p.Direction == PaymentDirection.Inbound
                        && p.Status == PaymentStatus.Pending
                        && p.InstallmentSeq != null
                        && p.DueDate != null
                        && p.DueDate < today
                        && p.PolicyId != null)
            .OrderBy(p => p.DueDate)
            .Select(p => new
            {
                p.Id,
                p.PaymentNo,
                PolicyId = p.PolicyId!.Value,
                PolicyNo = p.Policy!.PolicyNo,
                CustomerName = p.Policy!.Customer.FullName,
                p.Policy!.Customer.Email,
                p.Policy!.Customer.Phone,
                InstallmentSeq = p.InstallmentSeq!.Value,
                p.Amount,
                DueDate = p.DueDate!.Value,
                LastRemindedAt = _db.Notifications.Where(n => n.PolicyId == p.PolicyId).Max(n => (DateTime?)n.SentAt),
            })
            .ToListAsync(ct);

        Response = rows.Select(r => new OverdueInstallmentDto(
            r.Id, r.PaymentNo, r.PolicyId, r.PolicyNo, r.CustomerName, r.Email, r.Phone,
            r.InstallmentSeq, r.Amount, r.DueDate, today.DayNumber - r.DueDate.DayNumber, r.LastRemindedAt)).ToList();
    }
}

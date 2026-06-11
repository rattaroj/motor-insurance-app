using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Payments;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Payments;

public record InstallmentReminderResponse(long NotificationId, string Channel, string Recipient, string Status);

/// <summary>
/// POST /api/payments/{id}/remind — record + dispatch an overdue-installment reminder to the
/// policy's customer (Line → email → SMS → log). Delivery goes through INotificationSender.
/// </summary>
public class SendInstallmentReminderEndpoint : EndpointWithoutRequest<InstallmentReminderResponse>
{
    private readonly IAppDbContext _db;
    private readonly INotificationSender _sender;
    private readonly IDateTimeProvider _clock;
    public SendInstallmentReminderEndpoint(IAppDbContext db, INotificationSender sender, IDateTimeProvider clock)
        => (_db, _sender, _clock) = (db, sender, clock);

    public override void Configure()
    {
        Post("payments/{id}/remind");
        Policies(PermissionPolicy.For(Perms.PaymentRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");

        var p = await _db.Payments.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.PolicyId,
                x.Direction,
                x.Status,
                x.InstallmentSeq,
                x.Amount,
                x.DueDate,
                PolicyNo = x.Policy != null ? x.Policy.PolicyNo : null,
                Name = x.Policy != null ? x.Policy.Customer.FullName : null,
                Email = x.Policy != null ? x.Policy.Customer.Email : null,
                Phone = x.Policy != null ? x.Policy.Customer.Phone : null,
                LineUserId = x.Policy != null ? x.Policy.Customer.LineUserId : null,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Payment), id);

        if (p.Direction != PaymentDirection.Inbound || p.Status != PaymentStatus.Pending
            || p.InstallmentSeq is null || p.PolicyId is null || p.PolicyNo is null)
            throw new ConflictException("เตือนได้เฉพาะงวดผ่อนเบี้ยที่ยังค้างชำระเท่านั้น");

        var note = await InstallmentReminders.SendAsync(
            _db, _sender, _clock, p.PolicyId.Value, p.PolicyNo, p.Name ?? "-", p.Email, p.Phone,
            p.InstallmentSeq.Value, p.Amount, p.DueDate, ct, p.LineUserId);

        Response = new InstallmentReminderResponse(note.Id, note.Channel, note.Recipient, note.Status);
    }
}

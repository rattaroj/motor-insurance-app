using System.Globalization;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Renewals;

public record SendReminderResponse(long NotificationId, string Channel, string Recipient, string Status);

/// <summary>
/// POST /api/renewals/{policyId}/remind — record + dispatch a renewal reminder to the customer
/// (prefers email, then phone/SMS, else a logged record). Delivery goes through INotificationSender.
/// </summary>
public class SendRenewalReminderEndpoint : EndpointWithoutRequest<SendReminderResponse>
{
    private static readonly CultureInfo Th = CultureInfo.GetCultureInfo("th-TH");

    private readonly IAppDbContext _db;
    private readonly INotificationSender _sender;
    private readonly IDateTimeProvider _clock;
    public SendRenewalReminderEndpoint(IAppDbContext db, INotificationSender sender, IDateTimeProvider clock)
        => (_db, _sender, _clock) = (db, sender, clock);

    public override void Configure()
    {
        Post("renewals/{policyId}/remind");
        Policies(PermissionPolicy.For(Perms.PolicyRenew));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var policyId = Route<long>("policyId");

        var p = await _db.Policies.AsNoTracking()
            .Where(x => x.Id == policyId)
            .Select(x => new
            {
                x.PolicyNo, x.ExpiryDate,
                Name = x.Customer.FullName, x.Customer.Email, x.Customer.Phone,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Policy), policyId);

        var (channel, recipient) = !string.IsNullOrWhiteSpace(p.Email) ? ("Email", p.Email!)
            : !string.IsNullOrWhiteSpace(p.Phone) ? ("Sms", p.Phone!)
            : ("Log", "-");

        var expiry = p.ExpiryDate?.ToString("dd/MM/yyyy", Th) ?? "-";
        var subject = $"แจ้งเตือนต่ออายุกรมธรรม์ {p.PolicyNo}";
        var body = $"เรียน {p.Name}\nกรมธรรม์เลขที่ {p.PolicyNo} จะหมดอายุวันที่ {expiry} " +
                   "กรุณาติดต่อเจ้าหน้าที่เพื่อต่ออายุความคุ้มครอง";

        var ok = await _sender.SendAsync(new NotificationMessage(channel, recipient, subject, body), ct);

        var note = new Notification
        {
            PolicyId = policyId,
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = ok ? "Sent" : "Failed",
            SentAt = ok ? _clock.UtcNow : null,
            CreatedAt = _clock.UtcNow,
        };
        _db.Notifications.Add(note);
        await _db.SaveChangesAsync(ct);

        Response = new SendReminderResponse(note.Id, channel, recipient, note.Status);
    }
}

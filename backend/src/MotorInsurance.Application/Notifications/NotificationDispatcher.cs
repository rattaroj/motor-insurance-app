using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Renewals;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Notifications;

/// <summary>
/// Generic event notifications to a policy's customer (policy issued, claim approved, …).
/// Resolves the contact channel, dispatches via <see cref="INotificationSender"/>, and records
/// the result in the notification table — the same shape as renewal reminders, reused across events.
/// </summary>
public static class NotificationDispatcher
{
    public static async Task<Notification?> SendToPolicyCustomerAsync(
        IAppDbContext db, INotificationSender sender, IDateTimeProvider clock,
        long policyId, string subject, string body, CancellationToken ct = default)
    {
        var contact = await db.Policies.AsNoTracking()
            .Where(p => p.Id == policyId)
            .Select(p => new { p.Customer.Email, p.Customer.Phone, p.Customer.LineUserId })
            .FirstOrDefaultAsync(ct);
        if (contact is null) return null;

        var (channel, recipient) = RenewalReminders.PickChannel(contact.Email, contact.Phone, contact.LineUserId);
        var ok = await sender.SendAsync(new NotificationMessage(channel, recipient, subject, body), ct);

        var note = new Notification
        {
            PolicyId = policyId,
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = ok ? "Sent" : "Failed",
            SentAt = ok ? clock.UtcNow : null,
            CreatedAt = clock.UtcNow,
        };
        db.Notifications.Add(note);
        await db.SaveChangesAsync(ct);
        return note;
    }
}

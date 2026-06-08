using System.Globalization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Renewals;

/// <summary>
/// Shared renewal-reminder logic: pick the channel (email → phone/SMS → log), compose the
/// Thai message, dispatch via <see cref="INotificationSender"/>, and record it in the
/// notification table. Used by both the manual remind endpoint and the background worker
/// (CLAUDE.md: logic shared by two call sites lives in Application as a plain helper).
/// </summary>
public static class RenewalReminders
{
    private static readonly CultureInfo Th = CultureInfo.GetCultureInfo("th-TH");

    public static (string Channel, string Recipient) PickChannel(string? email, string? phone, string? lineUserId = null) =>
        !string.IsNullOrWhiteSpace(lineUserId) ? ("Line", lineUserId!)
        : !string.IsNullOrWhiteSpace(email) ? ("Email", email!)
        : !string.IsNullOrWhiteSpace(phone) ? ("Sms", phone!)
        : ("Log", "-");

    /// <summary>Dispatch + persist a renewal reminder for one policy; returns the saved record.</summary>
    public static async Task<Notification> SendAsync(
        IAppDbContext db, INotificationSender sender, IDateTimeProvider clock,
        long policyId, string policyNo, string customerName, string? email, string? phone, DateOnly? expiry,
        CancellationToken ct, string? lineUserId = null)
    {
        var (channel, recipient) = PickChannel(email, phone, lineUserId);

        var expiryText = expiry?.ToString("dd/MM/yyyy", Th) ?? "-";
        var subject = $"แจ้งเตือนต่ออายุกรมธรรม์ {policyNo}";
        var body = $"เรียน {customerName}\nกรมธรรม์เลขที่ {policyNo} จะหมดอายุวันที่ {expiryText} " +
                   "กรุณาติดต่อเจ้าหน้าที่เพื่อต่ออายุความคุ้มครอง";

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

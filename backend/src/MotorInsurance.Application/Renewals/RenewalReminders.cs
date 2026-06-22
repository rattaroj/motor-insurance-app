using System.Globalization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Notifications;
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

    /// <summary>
    /// Dispatch + persist a renewal reminder for one policy; returns the saved record.
    /// When <paramref name="estimatedPremium"/> is supplied, the quoted renewal price is appended
    /// to the message so the customer sees what renewing will cost.
    /// </summary>
    public static async Task<Notification> SendAsync(
        IAppDbContext db, INotificationSender sender, IDateTimeProvider clock,
        long policyId, string policyNo, string customerName, string? email, string? phone, DateOnly? expiry,
        CancellationToken ct, string? lineUserId = null, decimal? estimatedPremium = null)
    {
        var (channel, recipient) = PickChannel(email, phone, lineUserId);

        var expiryText = expiry?.ToString("dd/MM/yyyy", Th) ?? "-";

        // Quote the price only when we actually have one; otherwise the whole "เบี้ยต่ออายุ" line
        // (label included) is omitted via the empty {{premiumLine}} token — no misleading placeholder.
        var premiumText = "ยืนยันเมื่อออกกรมธรรม์";
        var premiumLine = "";
        if (estimatedPremium is { } premium)
        {
            premiumText = $"{premium.ToString("N2", Th)} บาท (ราคาจริงยืนยันเมื่อออกกรมธรรม์)";
            premiumLine = $"\nเบี้ยต่ออายุโดยประมาณ {premiumText}";
        }

        var vars = new Dictionary<string, string>
        {
            ["customerName"] = customerName,
            ["policyNo"] = policyNo,
            ["expiryDate"] = expiryText,
            ["premiumLine"] = premiumLine,        // full line incl. label, empty when no quote
            ["estimatedPremium"] = premiumText,   // legacy token, for templates predating premiumLine
        };
        var (subject, body) = await NotificationTemplates.RenderAsync(
            db, "renewal", vars,
            "แจ้งเตือนต่ออายุกรมธรรม์ {{policyNo}}",
            "เรียน {{customerName}}\nกรมธรรม์เลขที่ {{policyNo}} จะหมดอายุวันที่ {{expiryDate}} " +
            "กรุณาติดต่อเจ้าหน้าที่เพื่อต่ออายุความคุ้มครอง{{premiumLine}}", ct);

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

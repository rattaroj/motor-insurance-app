using System.Globalization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Notifications;
using MotorInsurance.Application.Renewals;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Payments;

/// <summary>
/// Shared overdue-installment reminder logic: pick the channel (Line → email → phone/SMS → log),
/// compose the Thai dunning message, dispatch via <see cref="INotificationSender"/> and record it
/// in the notification table. Mirrors <see cref="RenewalReminders"/> (CLAUDE.md: logic shared by
/// two call sites lives in Application as a plain helper).
/// </summary>
public static class InstallmentReminders
{
    private static readonly CultureInfo Th = CultureInfo.GetCultureInfo("th-TH");

    /// <summary>Dispatch + persist an overdue-installment reminder for one payment; returns the saved record.</summary>
    public static async Task<Notification> SendAsync(
        IAppDbContext db, INotificationSender sender, IDateTimeProvider clock,
        long policyId, string policyNo, string customerName, string? email, string? phone,
        int installmentSeq, decimal amount, DateOnly? dueDate, CancellationToken ct, string? lineUserId = null)
    {
        var (channel, recipient) = RenewalReminders.PickChannel(email, phone, lineUserId);

        var dueText = dueDate?.ToString("dd/MM/yyyy", Th) ?? "-";
        var vars = new Dictionary<string, string>
        {
            ["customerName"] = customerName,
            ["policyNo"] = policyNo,
            ["installmentSeq"] = installmentSeq.ToString(),
            ["amount"] = amount.ToString("N2", Th),
            ["dueDate"] = dueText,
        };
        var (subject, body) = await NotificationTemplates.RenderAsync(
            db, "installment", vars,
            "แจ้งเตือนชำระเบี้ยงวดที่ {{installmentSeq}} กรมธรรม์ {{policyNo}}",
            "เรียน {{customerName}}\n" +
            "เบี้ยประกันงวดที่ {{installmentSeq}} ของกรมธรรม์เลขที่ {{policyNo}} " +
            "จำนวน {{amount}} บาท ครบกำหนดชำระเมื่อวันที่ {{dueDate}} และยังไม่ได้รับชำระ\n" +
            "กรุณาชำระโดยเร็วเพื่อรักษาความคุ้มครองของกรมธรรม์", ct);

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

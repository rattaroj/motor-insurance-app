using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Renewals;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Policies;

/// <summary>
/// Proactive installment-due reminders: notifies the customer a few days <em>before</em> a pending
/// installment's due date so they can pay before the policy is suspended (the worker's
/// <c>SuspendOverdueAsync</c> only acts <em>after</em> overdue). Reuses the renewal-reminder channel
/// picker + notification recording. Idempotent: a reminder is keyed by its (policy, installment,
/// days-left) subject, so re-running the same day never double-sends.
/// </summary>
public static class InstallmentReminders
{
    private static readonly CultureInfo Th = CultureInfo.GetCultureInfo("th-TH");

    /// <summary>
    /// Send reminders for installments whose due date is exactly <paramref name="reminderDays"/> days
    /// ahead of <paramref name="today"/> (e.g. 7/3/1). Returns the number of reminders dispatched.
    /// </summary>
    public static async Task<int> SendDueRemindersAsync(
        IAppDbContext db, INotificationSender sender, IDateTimeProvider clock,
        IReadOnlyList<int> reminderDays, DateOnly today, CancellationToken ct = default)
    {
        if (reminderDays is null || reminderDays.Count == 0) return 0;

        // Only future, not-yet-overdue due dates that fall on one of the reminder offsets.
        var targetDates = reminderDays.Where(d => d > 0).Select(today.AddDays).Distinct().ToList();
        if (targetDates.Count == 0) return 0;

        var due = await db.Payments.AsNoTracking()
            .Where(p => p.InstallmentPlanId != null
                && p.Status == PaymentStatus.Pending
                && p.DueDate != null
                && targetDates.Contains(p.DueDate.Value)
                && p.Policy!.Status == PolicyStatus.Active)
            .Select(p => new
            {
                p.PolicyId,
                p.Policy!.PolicyNo,
                p.InstallmentSeq,
                p.Amount,
                DueDate = p.DueDate!.Value,
                Name = p.Policy.Customer.FullName,
                p.Policy.Customer.Email,
                p.Policy.Customer.Phone,
                p.Policy.Customer.LineUserId,
            })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var inst in due)
        {
            var daysLeft = inst.DueDate.DayNumber - today.DayNumber;
            var subject = $"แจ้งเตือนชำระค่าเบี้ยงวดที่ {inst.InstallmentSeq} กรมธรรม์ {inst.PolicyNo} (อีก {daysLeft} วัน)";

            // Idempotent across re-runs: the subject uniquely identifies this (policy, seq, days-left) reminder.
            var already = await db.Notifications.AsNoTracking()
                .AnyAsync(n => n.PolicyId == inst.PolicyId && n.Subject == subject, ct);
            if (already) continue;

            var dueText = inst.DueDate.ToString("dd/MM/yyyy", Th);
            var body = $"เรียน {inst.Name}\nกรมธรรม์ {inst.PolicyNo} มีงวดผ่อนเบี้ยงวดที่ {inst.InstallmentSeq} " +
                       $"จำนวน {inst.Amount.ToString("N2", Th)} บาท ครบกำหนดชำระวันที่ {dueText} " +
                       "กรุณาชำระภายในกำหนดเพื่อคงความคุ้มครอง";

            var (channel, recipient) = RenewalReminders.PickChannel(inst.Email, inst.Phone, inst.LineUserId);
            var ok = await sender.SendAsync(new NotificationMessage(channel, recipient, subject, body), ct);

            db.Notifications.Add(new Notification
            {
                PolicyId = inst.PolicyId,
                Channel = channel,
                Recipient = recipient,
                Subject = subject,
                Body = body,
                Status = ok ? "Sent" : "Failed",
                SentAt = ok ? clock.UtcNow : null,
                CreatedAt = clock.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            sent++;
        }

        return sent;
    }
}

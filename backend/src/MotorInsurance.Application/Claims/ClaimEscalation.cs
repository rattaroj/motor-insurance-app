using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Claims;

/// <summary>
/// SLA auto-escalation: finds open claims that have sat in their current status longer than the
/// <see cref="ClaimSla"/> target and alerts the claims supervisors (active users whose role grants
/// <see cref="Permissions.ClaimApprove"/>). The aging worklist (<c>ClaimsAgingEndpoint</c>) only
/// <em>shows</em> breaches; this pass <em>pushes</em> them so a stuck claim can't sit unseen.
/// Idempotent: at most one escalation per claim per status-entry — keyed by the alert subject plus
/// the moment the claim entered its current status, so a later status change re-arms the alert.
/// </summary>
public static class ClaimEscalation
{
    private static readonly CultureInfo Th = CultureInfo.GetCultureInfo("th-TH");

    /// <summary>Alert every breached open claim to the claim supervisors; returns the count escalated.</summary>
    public static async Task<int> SendEscalationsAsync(
        IAppDbContext db, IClaimAgingReader reader, INotificationSender sender,
        IDateTimeProvider clock, CancellationToken ct = default)
    {
        var today = clock.UtcNow.Date;
        var open = await reader.GetOpenAsync(ct);

        var breached = open
            .Where(r => Math.Max(0, (today - r.StatusSince.Date).Days) > ClaimSla.DaysFor(r.Status))
            .ToList();
        if (breached.Count == 0) return 0;

        // Recipients: active users whose role can approve claims (the claims supervisors).
        var supervisors = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Email != null && u.Email != ""
                && u.UserRoles.Any(ur => ur.Role.RolePermissions.Any(rp => rp.PermissionCode == Permissions.ClaimApprove)))
            .Select(u => u.Email)
            .Distinct()
            .ToListAsync(ct);

        var recipient = supervisors.Count > 0 ? string.Join("; ", supervisors) : "claims-supervisors";

        var sent = 0;
        foreach (var r in breached)
        {
            var subject = $"[SLA] เคลม {r.ClaimNo} ค้างเกินกำหนด";

            // Idempotent per status-entry: a prior escalation counts only if it was raised after the
            // claim entered its current status. A status change moves StatusSince forward, re-arming it.
            var already = await db.Notifications.AsNoTracking()
                .AnyAsync(n => n.Subject == subject && n.CreatedAt >= r.StatusSince, ct);
            if (already) continue;

            var days = Math.Max(0, (today - r.StatusSince.Date).Days);
            var body =
                $"เคลมเลขที่ {r.ClaimNo} (กรมธรรม์ {r.PolicyNo}) อยู่ในสถานะ {r.Status} มา {days} วัน " +
                $"เกินเป้าหมาย SLA {ClaimSla.DaysFor(r.Status)} วัน\n" +
                $"จำนวนเงินเรียกร้อง {r.ClaimedAmount.ToString("N2", Th)} บาท กรุณาตรวจสอบและเร่งดำเนินการ";

            var ok = await sender.SendAsync(new NotificationMessage("Email", recipient, subject, body), ct);

            db.Notifications.Add(new Notification
            {
                PolicyId = null,
                Channel = "Email",
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

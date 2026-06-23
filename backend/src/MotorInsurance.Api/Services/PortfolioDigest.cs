using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Endpoints.Reports;
using MotorInsurance.Application.Common.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Api.Services;

/// <summary>
/// Weekly portfolio digest: emails a one-page snapshot of last week's portfolio health (premium
/// written, claims paid, loss ratio, new policies, and status/coverage breakdowns) to the people who
/// can read the dashboard (active users whose role grants <see cref="Permissions.DashboardRead"/>).
/// Reuses <see cref="AnalyticsQuery"/> so the figures match the reporting dashboard exactly.
/// Idempotent: at most one digest per week — keyed by the week-ending date baked into the subject,
/// so the worker firing several times on the send day produces a single email.
/// </summary>
public static class PortfolioDigest
{
    private static readonly CultureInfo Th = CultureInfo.GetCultureInfo("th-TH");

    /// <summary>Send the digest for the week ending yesterday; returns 1 if sent, 0 if skipped.</summary>
    public static async Task<int> SendWeeklyDigestAsync(
        IAppDbContext db, IDateTimeProvider clock, INotificationSender sender, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.Date);
        var from = today.AddDays(-7);   // last 7 complete days …
        var to = today.AddDays(-1);     // … ending yesterday.

        // Idempotent per week: the week-ending date in the subject is the dedupe key. Invariant
        // (Gregorian) formatting keeps the key stable regardless of the host's Thai/Buddhist culture.
        var subject = $"[สรุปพอร์ต] รายงานประจำสัปดาห์ถึง {to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        if (await db.Notifications.AsNoTracking().AnyAsync(n => n.Subject == subject, ct)) return 0;

        var dto = await AnalyticsQuery.ComputeAsync(db, clock, from, to, ct);

        // Recipients: active users whose role can read the dashboard (the portfolio audience).
        var readers = await db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Email != null && u.Email != ""
                && u.UserRoles.Any(ur => ur.Role.RolePermissions.Any(rp => rp.PermissionCode == Permissions.DashboardRead)))
            .Select(u => u.Email)
            .Distinct()
            .ToListAsync(ct);

        var recipient = readers.Count > 0 ? string.Join("; ", readers) : "portfolio-managers";
        var body = BuildBody(from, to, dto);

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
        return 1;
    }

    private static string BuildBody(DateOnly from, DateOnly to, AnalyticsDto d)
    {
        var newPolicies = d.PoliciesByStatus.Sum(x => x.Count);
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine($"สรุปภาพรวมพอร์ตระหว่างวันที่ {from.ToString("dd/MM/yyyy", inv)} ถึง {to.ToString("dd/MM/yyyy", inv)}");
        sb.AppendLine();
        sb.AppendLine($"• เบี้ยรับรวม: {d.PremiumWritten.ToString("N2", Th)} บาท");
        sb.AppendLine($"• ค่าสินไหมที่จ่าย: {d.ClaimsPaid.ToString("N2", Th)} บาท");
        sb.AppendLine($"• Loss ratio: {d.LossRatio.ToString("P1", Th)}");
        sb.AppendLine($"• กรมธรรม์ใหม่: {newPolicies} ฉบับ");
        sb.AppendLine();
        AppendBreakdown(sb, "กรมธรรม์ใหม่แยกตามประเภทความคุ้มครอง", d.PoliciesByCoverage);
        AppendBreakdown(sb, "กรมธรรม์ใหม่แยกตามสถานะ", d.PoliciesByStatus);
        AppendBreakdown(sb, "เคลมที่เปิดในสัปดาห์แยกตามสถานะ", d.ClaimsByStatus);
        return sb.ToString().TrimEnd();
    }

    private static void AppendBreakdown(StringBuilder sb, string heading, IReadOnlyList<LabelCount> rows)
    {
        sb.AppendLine($"{heading}:");
        if (rows.Count == 0)
        {
            sb.AppendLine("  - ไม่มีรายการ");
        }
        else
        {
            foreach (var r in rows.OrderByDescending(x => x.Count))
                sb.AppendLine($"  - {r.Label}: {r.Count}");
        }
        sb.AppendLine();
    }
}

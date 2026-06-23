using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MotorInsurance.Application.Claims;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Notifications;
using MotorInsurance.Application.Policies;
using MotorInsurance.Application.Renewals;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Domain.StateMachines;

namespace MotorInsurance.Api.Services;

/// <summary>Configuration for <see cref="PolicyLifecycleWorker"/> (section "PolicyLifecycle").</summary>
public class PolicyLifecycleOptions
{
    public bool Enabled { get; set; } = true;
    public bool AutoExpire { get; set; } = true;
    public bool AutoRemind { get; set; } = true;
    public bool AutoSuspendOverdue { get; set; } = true;
    /// <summary>Send installment-due reminders ahead of the due date (see <see cref="InstallmentReminderDays"/>).</summary>
    public bool AutoRemindInstallments { get; set; } = true;
    /// <summary>Alert claim supervisors when an open claim breaches its per-status SLA.</summary>
    public bool AutoEscalateClaims { get; set; } = true;
    /// <summary>Email a weekly portfolio digest to dashboard readers (once per week, on <see cref="DigestDayOfWeek"/>).</summary>
    public bool AutoWeeklyDigest { get; set; } = true;
    /// <summary>Day of week the weekly portfolio digest is sent (default Monday).</summary>
    public DayOfWeek DigestDayOfWeek { get; set; } = DayOfWeek.Monday;
    public double RunIntervalHours { get; set; } = 6;
    /// <summary>How far ahead to look for expiring policies when auto-reminding.</summary>
    public int ReminderWindowDays { get; set; } = 60;
    /// <summary>Don't re-remind a policy that was reminded within this many days.</summary>
    public int ReminderThrottleDays { get; set; } = 7;
    /// <summary>Days ahead of an installment's due date to send a reminder (e.g. 7/3/1).</summary>
    public List<int> InstallmentReminderDays { get; set; } = new() { 7, 3, 1 };
}

/// <summary>
/// Periodic policy-lifecycle maintenance that closes two gaps the request flow never covers:
/// (1) flips Active policies past their expiry date to Expired (via the state machine), and
/// (2) auto-sends renewal reminders for policies in the pre-expiry window that haven't been
/// reminded recently and haven't been renewed. Idempotent: re-running does nothing new
/// (already-Expired policies are skipped; reminders are throttled by recent sends).
/// </summary>
public class PolicyLifecycleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PolicyLifecycleWorker> _log;
    private readonly PolicyLifecycleOptions _opt;

    public PolicyLifecycleWorker(
        IServiceScopeFactory scopes, ILogger<PolicyLifecycleWorker> log, IOptions<PolicyLifecycleOptions> opt)
        => (_scopes, _log, _opt) = (scopes, log, opt.Value);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_opt.Enabled)
        {
            _log.LogInformation("PolicyLifecycleWorker disabled by configuration.");
            return;
        }

        // Small startup delay so the host finishes booting + seeding before the first run.
        if (!await DelayAsync(TimeSpan.FromSeconds(15), ct)) return;

        var interval = TimeSpan.FromHours(Math.Max(0.1, _opt.RunIntervalHours));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a bad run kill the worker — log and retry next interval.
                _log.LogError(ex, "Policy lifecycle run failed.");
            }

            if (!await DelayAsync(interval, ct)) break;
        }
    }

    /// <summary>One maintenance pass. Public so it can be unit/integration-tested directly.</summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationSender>();
        var today = DateOnly.FromDateTime(clock.UtcNow.Date);

        if (_opt.AutoExpire) await ExpireAsync(db, today, ct);
        if (_opt.AutoRemindInstallments)
        {
            var qr = scope.ServiceProvider.GetRequiredService<IPromptPayQrGenerator>();
            await RemindInstallmentsAsync(db, sender, clock, today, qr, ct);
        }
        if (_opt.AutoSuspendOverdue) await SuspendOverdueAsync(db, sender, clock, today, ct);
        if (_opt.AutoRemind) await RemindAsync(db, sender, clock, today, ct);
        if (_opt.AutoEscalateClaims)
        {
            var reader = scope.ServiceProvider.GetRequiredService<IClaimAgingReader>();
            await EscalateClaimsAsync(db, reader, sender, clock, ct);
        }
        if (_opt.AutoWeeklyDigest && today.DayOfWeek == _opt.DigestDayOfWeek)
            await SendWeeklyDigestAsync(db, sender, clock, ct);
    }

    /// <summary>
    /// Emails the weekly portfolio digest to dashboard readers. Gated to <see cref="PolicyLifecycleOptions.DigestDayOfWeek"/>
    /// in <see cref="RunOnceAsync"/>; <see cref="PortfolioDigest"/> is itself idempotent per week, so the worker
    /// firing several times on that day still yields one email.
    /// </summary>
    private async Task SendWeeklyDigestAsync(
        IAppDbContext db, INotificationSender sender, IDateTimeProvider clock, CancellationToken ct)
    {
        var n = await PortfolioDigest.SendWeeklyDigestAsync(db, clock, sender, ct);
        if (n > 0) _log.LogInformation("Sent weekly portfolio digest.");
    }

    /// <summary>
    /// Pushes SLA-breached open claims to the claims supervisors so a stuck claim can't sit unseen
    /// in the aging worklist. Delegates to <see cref="ClaimEscalation"/> (idempotent per status-entry).
    /// </summary>
    private async Task EscalateClaimsAsync(
        IAppDbContext db, IClaimAgingReader reader, INotificationSender sender, IDateTimeProvider clock, CancellationToken ct)
    {
        var n = await ClaimEscalation.SendEscalationsAsync(db, reader, sender, clock, ct);
        if (n > 0) _log.LogInformation("Escalated {Count} SLA-breached claim(s).", n);
    }

    /// <summary>
    /// Reminds customers a few days before a pending installment falls due (offsets from
    /// <see cref="PolicyLifecycleOptions.InstallmentReminderDays"/>), closing the loop that
    /// <see cref="SuspendOverdueAsync"/> only handles after the fact. Idempotent via the reminder subject.
    /// </summary>
    private async Task RemindInstallmentsAsync(
        IAppDbContext db, INotificationSender sender, IDateTimeProvider clock, DateOnly today,
        IPromptPayQrGenerator qr, CancellationToken ct)
    {
        var n = await InstallmentReminders.SendDueRemindersAsync(
            db, sender, clock, _opt.InstallmentReminderDays, today, ct, qr);
        if (n > 0) _log.LogInformation("Auto-sent {Count} installment-due reminder(s).", n);
    }

    /// <summary>
    /// Suspends Active policies that have a past-due, still-pending installment (and marks the plan
    /// Defaulted), then notifies the customer. Reversed automatically when the overdue installment is
    /// settled (see SettlePaymentEndpoint). Idempotent: an already-Suspended policy is not re-picked.
    /// </summary>
    private async Task SuspendOverdueAsync(
        IAppDbContext db, INotificationSender sender, IDateTimeProvider clock, DateOnly today, CancellationToken ct)
    {
        var overduePolicyIds = await db.Payments.AsNoTracking()
            .Where(p => p.InstallmentPlanId != null
                && p.Status == PaymentStatus.Pending
                && p.DueDate != null && p.DueDate < today
                && p.Policy!.Status == PolicyStatus.Active)
            .Select(p => p.PolicyId!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (overduePolicyIds.Count == 0) return;

        var policies = await db.Policies.Where(p => overduePolicyIds.Contains(p.Id)).ToListAsync(ct);
        foreach (var policy in policies)
        {
            PolicyStateMachine.EnsureTransition(policy.Status, PolicyStatus.Suspended);
            policy.Status = PolicyStatus.Suspended;
        }

        var plans = await db.InstallmentPlans
            .Where(ip => overduePolicyIds.Contains(ip.PolicyId) && ip.Status == InstallmentPlanStatus.Active)
            .ToListAsync(ct);
        foreach (var plan in plans) plan.Status = InstallmentPlanStatus.Defaulted;

        await db.SaveChangesAsync(ct);

        foreach (var policy in policies)
            await NotificationDispatcher.SendToPolicyCustomerAsync(
                db, sender, clock, policy.Id,
                $"กรมธรรม์ {policy.PolicyNo} ถูกระงับชั่วคราว",
                "เนื่องจากมีงวดผ่อนเบี้ยเกินกำหนดชำระ กรุณาชำระเพื่อเปิดความคุ้มครองอีกครั้ง", ct);

        _log.LogInformation("Suspended {Count} policy(ies) for overdue installments.", policies.Count);
    }

    private async Task ExpireAsync(IAppDbContext db, DateOnly today, CancellationToken ct)
    {
        var due = await db.Policies
            .Where(p => p.Status == PolicyStatus.Active && p.ExpiryDate != null && p.ExpiryDate < today)
            .ToListAsync(ct);

        foreach (var p in due)
        {
            PolicyStateMachine.EnsureTransition(p.Status, PolicyStatus.Expired);
            p.Status = PolicyStatus.Expired;
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            _log.LogInformation("Auto-expired {Count} policy(ies).", due.Count);
        }
    }

    private async Task RemindAsync(
        IAppDbContext db, INotificationSender sender, IDateTimeProvider clock, DateOnly today, CancellationToken ct)
    {
        var throttleSince = clock.UtcNow.AddDays(-_opt.ReminderThrottleDays);

        var due = await db.Policies.AsNoTracking()
            .ExpiringWithin(today, _opt.ReminderWindowDays)
            .NotYetRenewed(db)
            .Where(p => !db.Notifications.Any(n => n.PolicyId == p.Id && n.SentAt != null && n.SentAt >= throttleSince))
            .Select(p => new
            {
                p.Id,
                p.PolicyNo,
                p.ExpiryDate,
                Name = p.Customer.FullName,
                p.Customer.Email,
                p.Customer.Phone,
                p.Customer.LineUserId,
            })
            .ToListAsync(ct);

        foreach (var p in due)
        {
            // Quote the renewal so the reminder shows the price (same rating the actual renewal uses).
            var quote = await RenewalQuote.EstimateAsync(db, p.Id, ct);
            await RenewalReminders.SendAsync(
                db, sender, clock, p.Id, p.PolicyNo, p.Name, p.Email, p.Phone, p.ExpiryDate, ct, p.LineUserId,
                quote?.Breakdown.NetPremium);
        }

        if (due.Count > 0)
            _log.LogInformation("Auto-sent {Count} renewal reminder(s).", due.Count);
    }

    /// <summary>Delay that swallows cancellation; returns false if cancelled.</summary>
    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

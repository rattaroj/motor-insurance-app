using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MotorInsurance.Application.Common.Interfaces;
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
    public double RunIntervalHours { get; set; } = 6;
    /// <summary>How far ahead to look for expiring policies when auto-reminding.</summary>
    public int ReminderWindowDays { get; set; } = 60;
    /// <summary>Don't re-remind a policy that was reminded within this many days.</summary>
    public int ReminderThrottleDays { get; set; } = 7;
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
        if (_opt.AutoRemind) await RemindAsync(db, sender, clock, today, ct);
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
        var limit = today.AddDays(_opt.ReminderWindowDays);
        var throttleSince = clock.UtcNow.AddDays(-_opt.ReminderThrottleDays);

        var due = await db.Policies.AsNoTracking()
            .Where(p => p.Status == PolicyStatus.Active
                && p.ExpiryDate != null && p.ExpiryDate >= today && p.ExpiryDate <= limit
                && !db.Policies.Any(r => r.PreviousPolicyId == p.Id)
                && !db.Notifications.Any(n => n.PolicyId == p.Id && n.SentAt != null && n.SentAt >= throttleSince))
            .Select(p => new
            {
                p.Id, p.PolicyNo, p.ExpiryDate,
                Name = p.Customer.FullName, p.Customer.Email, p.Customer.Phone,
            })
            .ToListAsync(ct);

        foreach (var p in due)
            await RenewalReminders.SendAsync(
                db, sender, clock, p.Id, p.PolicyNo, p.Name, p.Email, p.Phone, p.ExpiryDate, ct);

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

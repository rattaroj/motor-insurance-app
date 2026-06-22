using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Policies;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// Proactive installment-due reminders: fires on the configured day-offsets before a pending
/// installment's due date, only for Active policies, and is idempotent across re-runs. (F1)
/// </summary>
public class InstallmentRemindersTests
{
    private static readonly DateOnly Today = new(2026, 6, 9);
    private static readonly int[] Offsets = { 7, 3, 1 };

    private static InMemoryAppDb SeedPolicyWithInstallment(
        DateOnly dueDate, PolicyStatus status = PolicyStatus.Active,
        PaymentStatus paymentStatus = PaymentStatus.Pending, int seq = 2)
    {
        var db = InMemoryAppDb.New();
        db.Customers.Add(new Customer
        {
            Id = 1,
            NationalId = "1100000000001",
            FirstName = "สมหญิง",
            LastName = "ใจดี",
            FullName = "สมหญิง ใจดี",
            Email = "somying@example.com",
        });
        db.Policies.Add(new Policy
        {
            Id = 1,
            PolicyNo = "POL-2026-000001",
            CustomerId = 1,
            VehicleId = 1,
            Status = status,
        });
        db.InstallmentPlans.Add(new InstallmentPlan { Id = 1, PolicyId = 1, Status = InstallmentPlanStatus.Active });
        db.Payments.Add(new Payment
        {
            Id = 1,
            PaymentNo = "PAY-2026-000001",
            Direction = PaymentDirection.Inbound,
            Status = paymentStatus,
            PolicyId = 1,
            Amount = 3_000m,
            InstallmentPlanId = 1,
            InstallmentSeq = seq,
            DueDate = dueDate,
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Sends_a_reminder_when_due_in_a_reminder_window()
    {
        await using var db = SeedPolicyWithInstallment(Today.AddDays(3));
        var sender = new FakeNotificationSender(result: true);

        var n = await InstallmentReminders.SendDueRemindersAsync(
            db, sender, new FixedClockProvider(), Offsets, Today);

        Assert.Equal(1, n);
        var note = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal("Email", note.Channel);
        Assert.Equal("Sent", note.Status);
        Assert.Contains("อีก 3 วัน", note.Subject);
        Assert.Contains("งวดที่ 2", note.Subject);
    }

    [Fact]
    public async Task Does_not_remind_when_due_date_is_outside_every_window()
    {
        await using var db = SeedPolicyWithInstallment(Today.AddDays(5));   // 5 ∉ {7,3,1}
        var sender = new FakeNotificationSender();

        var n = await InstallmentReminders.SendDueRemindersAsync(
            db, sender, new FixedClockProvider(), Offsets, Today);

        Assert.Equal(0, n);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Skips_overdue_installments_left_to_the_suspend_pass()
    {
        await using var db = SeedPolicyWithInstallment(Today.AddDays(-1));  // already past due
        var sender = new FakeNotificationSender();

        var n = await InstallmentReminders.SendDueRemindersAsync(
            db, sender, new FixedClockProvider(), Offsets, Today);

        Assert.Equal(0, n);
    }

    [Fact]
    public async Task Ignores_already_paid_installments()
    {
        await using var db = SeedPolicyWithInstallment(Today.AddDays(1), paymentStatus: PaymentStatus.Paid);
        var sender = new FakeNotificationSender();

        var n = await InstallmentReminders.SendDueRemindersAsync(
            db, sender, new FixedClockProvider(), Offsets, Today);

        Assert.Equal(0, n);
    }

    [Fact]
    public async Task Ignores_non_active_policies()
    {
        await using var db = SeedPolicyWithInstallment(Today.AddDays(1), status: PolicyStatus.Suspended);
        var sender = new FakeNotificationSender();

        var n = await InstallmentReminders.SendDueRemindersAsync(
            db, sender, new FixedClockProvider(), Offsets, Today);

        Assert.Equal(0, n);
    }

    [Fact]
    public async Task Is_idempotent_across_re_runs_on_the_same_day()
    {
        await using var db = SeedPolicyWithInstallment(Today.AddDays(7));
        var sender = new FakeNotificationSender(result: true);
        var clock = new FixedClockProvider();

        var first = await InstallmentReminders.SendDueRemindersAsync(db, sender, clock, Offsets, Today);
        var second = await InstallmentReminders.SendDueRemindersAsync(db, sender, clock, Offsets, Today);

        Assert.Equal(1, first);
        Assert.Equal(0, second);   // not re-sent
        Assert.Single(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Reminds_again_at_the_next_offset_as_the_due_date_approaches()
    {
        await using var db = SeedPolicyWithInstallment(new DateOnly(2026, 6, 16));   // due in 7 days from Today
        var sender = new FakeNotificationSender(result: true);
        var clock = new FixedClockProvider();

        var atDay7 = await InstallmentReminders.SendDueRemindersAsync(db, sender, clock, Offsets, Today);
        var atDay3 = await InstallmentReminders.SendDueRemindersAsync(db, sender, clock, Offsets, new DateOnly(2026, 6, 13));

        Assert.Equal(1, atDay7);
        Assert.Equal(1, atDay3);   // distinct "days-left" subject → a second reminder
        Assert.Equal(2, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Attaches_a_promptpay_qr_for_email_reminders()
    {
        await using var db = SeedPolicyWithInstallment(Today.AddDays(3));   // customer has an email
        var sender = new FakeNotificationSender(result: true);
        var qr = new FakePromptPayQr();

        var n = await InstallmentReminders.SendDueRemindersAsync(
            db, sender, new FixedClockProvider(), Offsets, Today, default, qr);

        Assert.Equal(1, n);
        var msg = Assert.Single(sender.Sent);
        Assert.Equal("Email", msg.Channel);
        Assert.Equal("image/png", msg.AttachmentContentType);
        Assert.NotNull(msg.AttachmentBytes);
        Assert.Contains("พร้อมเพย์", msg.Body);
        Assert.Equal(3_000m, Assert.Single(qr.Amounts));   // QR encodes the installment amount
    }

    [Fact]
    public async Task Does_not_attach_a_qr_for_non_email_channels()
    {
        await using var db = InMemoryAppDb.New();
        // Customer with a LINE id but no email → routed to LINE, which carries no attachment.
        db.Customers.Add(new Customer
        {
            Id = 1, NationalId = "1100000000001", FirstName = "สมหญิง", LastName = "ใจดี",
            FullName = "สมหญิง ใจดี", LineUserId = "U123",
        });
        db.Policies.Add(new Policy
        {
            Id = 1, PolicyNo = "POL-2026-000001", CustomerId = 1, VehicleId = 1, Status = PolicyStatus.Active,
        });
        db.InstallmentPlans.Add(new InstallmentPlan { Id = 1, PolicyId = 1, Status = InstallmentPlanStatus.Active });
        db.Payments.Add(new Payment
        {
            Id = 1, PaymentNo = "PAY-2026-000001", Direction = PaymentDirection.Inbound,
            Status = PaymentStatus.Pending, PolicyId = 1, Amount = 3_000m,
            InstallmentPlanId = 1, InstallmentSeq = 2, DueDate = Today.AddDays(3),
        });
        db.SaveChanges();

        var sender = new FakeNotificationSender(result: true);
        var qr = new FakePromptPayQr();

        var n = await InstallmentReminders.SendDueRemindersAsync(
            db, sender, new FixedClockProvider(), Offsets, Today, default, qr);

        Assert.Equal(1, n);
        var msg = Assert.Single(sender.Sent);
        Assert.Equal("Line", msg.Channel);
        Assert.Null(msg.AttachmentBytes);
        Assert.Empty(qr.Amounts);                       // generator never invoked off-email
        Assert.DoesNotContain("พร้อมเพย์", msg.Body);
    }

    [Fact]
    public async Task Records_failed_when_delivery_fails()
    {
        await using var db = SeedPolicyWithInstallment(Today.AddDays(1));
        var sender = new FakeNotificationSender(result: false);

        var n = await InstallmentReminders.SendDueRemindersAsync(
            db, sender, new FixedClockProvider(), Offsets, Today);

        Assert.Equal(1, n);
        var note = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal("Failed", note.Status);
        Assert.Null(note.SentAt);
    }
}

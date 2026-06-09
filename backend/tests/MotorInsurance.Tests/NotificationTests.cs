using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Notifications;
using MotorInsurance.Application.Renewals;
using MotorInsurance.Domain.Entities;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// Notification channel selection, renewal-reminder recording, and generic event dispatch. (F4)
/// </summary>
public class NotificationTests
{
    [Theory]
    [InlineData("a@b.com", "0812345678", "Email", "a@b.com")]   // email preferred
    [InlineData(null, "0812345678", "Sms", "0812345678")]       // phone fallback
    [InlineData(null, null, "Log", "-")]                        // nothing → logged
    public void PickChannel_prefers_email_then_phone_then_log(
        string? email, string? phone, string expectedChannel, string expectedRecipient)
    {
        var (channel, recipient) = RenewalReminders.PickChannel(email, phone);
        Assert.Equal(expectedChannel, channel);
        Assert.Equal(expectedRecipient, recipient);
    }

    [Fact]
    public async Task RenewalReminder_records_a_sent_notification()
    {
        await using var db = InMemoryAppDb.New();
        var sender = new FakeNotificationSender(result: true);

        var note = await RenewalReminders.SendAsync(
            db, sender, new FixedClockProvider(), policyId: 1, policyNo: "POL-1",
            customerName: "สมชาย", email: "a@b.com", phone: null,
            expiry: new DateOnly(2026, 7, 1), ct: default);

        Assert.Equal("Email", note.Channel);
        Assert.Equal("Sent", note.Status);
        Assert.NotNull(note.SentAt);
        Assert.Single(sender.Sent);
        Assert.Single(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task RenewalReminder_marks_failed_when_delivery_fails()
    {
        await using var db = InMemoryAppDb.New();
        var sender = new FakeNotificationSender(result: false);

        var note = await RenewalReminders.SendAsync(
            db, sender, new FixedClockProvider(), 1, "POL-1", "สมชาย", "a@b.com", null,
            new DateOnly(2026, 7, 1), default);

        Assert.Equal("Failed", note.Status);
        Assert.Null(note.SentAt);
    }

    [Fact]
    public async Task Dispatcher_sends_to_the_policy_customers_channel()
    {
        await using var db = InMemoryAppDb.New();
        db.Customers.Add(new Customer
        {
            Id = 1,
            NationalId = "1100000000001",
            FirstName = "ก",
            LastName = "ข",
            FullName = "ก ข",
            Phone = "0899999999",   // no email → SMS channel
        });
        db.Policies.Add(new Policy { Id = 1, PolicyNo = "POL-1", CustomerId = 1, VehicleId = 1 });
        await db.SaveChangesAsync();
        var sender = new FakeNotificationSender(result: true);

        var note = await NotificationDispatcher.SendToPolicyCustomerAsync(
            db, sender, new FixedClockProvider(), 1, "ออกกรมธรรม์", "รายละเอียด", default);

        Assert.NotNull(note);
        Assert.Equal("Sms", note!.Channel);
        Assert.Equal("0899999999", note.Recipient);
        Assert.Equal("Sent", note.Status);
    }

    [Fact]
    public async Task Dispatcher_returns_null_when_policy_missing()
    {
        await using var db = InMemoryAppDb.New();
        var sender = new FakeNotificationSender();

        var note = await NotificationDispatcher.SendToPolicyCustomerAsync(
            db, sender, new FixedClockProvider(), 999, "x", "y", default);

        Assert.Null(note);
        Assert.Empty(sender.Sent);
    }
}

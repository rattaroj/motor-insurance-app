using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Services;
using MotorInsurance.Application.Common.Authorization;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// Weekly portfolio digest: emails last week's portfolio snapshot to dashboard readers, reusing
/// the analytics aggregation. Idempotent per week (keyed by the week-ending date in the subject).
/// </summary>
public class PortfolioDigestTests
{
    // A Monday; the digest covers 2026-06-08 .. 2026-06-14 (the prior 7 days, ending yesterday).
    private static readonly DateTime Day0 = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InWindow = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BeforeWindow = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static InMemoryAppDb SeedWithReader()
    {
        var db = InMemoryAppDb.New();
        db.Users.Add(new AppUser
        {
            Id = 1, Username = "analyst", Email = "analyst@example.com", FullName = "นักวิเคราะห์", PasswordHash = "x",
        });
        db.Roles.Add(new Role { Id = 1, Code = "MANAGER", NameTh = "ผู้จัดการ", NameEn = "Manager" });
        db.UserRoles.Add(new UserRole { UserId = 1, RoleId = 1 });
        db.RolePermissions.Add(new RolePermission { RoleId = 1, PermissionCode = Permissions.DashboardRead });
        db.SaveChanges();
        return db;
    }

    private static Policy PolicyCreatedAt(long id, DateTime createdAt, CoverageType coverage = CoverageType.Type1) =>
        new()
        {
            Id = id, PolicyNo = $"POL-2026-{id:D6}", CustomerId = 1, VehicleId = 1,
            Status = PolicyStatus.Active, CoverageType = coverage, Premium = 12_000m, CreatedAt = createdAt,
        };

    [Fact]
    public async Task Sends_a_digest_to_dashboard_readers_and_records_it()
    {
        await using var db = SeedWithReader();
        db.Policies.Add(PolicyCreatedAt(1, InWindow));
        await db.SaveChangesAsync();
        var sender = new FakeNotificationSender(result: true);

        var n = await PortfolioDigest.SendWeeklyDigestAsync(db, new FixedClockProvider(Day0), sender);

        Assert.Equal(1, n);
        var msg = Assert.Single(sender.Sent);
        Assert.Contains("analyst@example.com", msg.Recipient);
        Assert.Contains("2026-06-14", msg.Subject);      // week-ending date baked into subject
        Assert.Contains("เบี้ยรับรวม", msg.Body);
        Assert.Contains("12,000.00", msg.Body);          // the one in-window policy's premium
        var note = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal("Sent", note.Status);
    }

    [Fact]
    public async Task Counts_only_policies_created_within_the_week()
    {
        await using var db = SeedWithReader();
        db.Policies.Add(PolicyCreatedAt(1, InWindow));
        db.Policies.Add(PolicyCreatedAt(2, BeforeWindow));   // excluded: before the window
        await db.SaveChangesAsync();
        var sender = new FakeNotificationSender(result: true);

        await PortfolioDigest.SendWeeklyDigestAsync(db, new FixedClockProvider(Day0), sender);

        Assert.Contains("กรมธรรม์ใหม่: 1 ฉบับ", Assert.Single(sender.Sent).Body);
    }

    [Fact]
    public async Task Is_idempotent_within_the_week()
    {
        await using var db = SeedWithReader();
        var sender = new FakeNotificationSender(result: true);
        var clock = new FixedClockProvider(Day0);

        var first = await PortfolioDigest.SendWeeklyDigestAsync(db, clock, sender);
        var second = await PortfolioDigest.SendWeeklyDigestAsync(db, clock, sender);

        Assert.Equal(1, first);
        Assert.Equal(0, second);   // not re-sent for the same week
        Assert.Single(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Falls_back_to_a_placeholder_recipient_when_no_reader_exists()
    {
        await using var db = InMemoryAppDb.New();   // no users seeded
        var sender = new FakeNotificationSender(result: true);

        var n = await PortfolioDigest.SendWeeklyDigestAsync(db, new FixedClockProvider(Day0), sender);

        Assert.Equal(1, n);
        Assert.Equal("portfolio-managers", Assert.Single(sender.Sent).Recipient);
    }
}

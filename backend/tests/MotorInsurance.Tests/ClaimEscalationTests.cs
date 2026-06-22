using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Claims;
using MotorInsurance.Application.Common.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// SLA auto-escalation: alerts the claim supervisors when an open claim sits in its current status
/// past the <see cref="ClaimSla"/> target, idempotent per status-entry (re-arms on a status change).
/// </summary>
public class ClaimEscalationTests
{
    private static readonly DateTime Day0 = new(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Stub aging reader returning a fixed set of open claims (TemporalAll() lives in Infrastructure).</summary>
    private sealed class FakeAgingReader : IClaimAgingReader
    {
        private readonly IReadOnlyList<ClaimAgingRow> _rows;
        public FakeAgingReader(params ClaimAgingRow[] rows) => _rows = rows;
        public Task<IReadOnlyList<ClaimAgingRow>> GetOpenAsync(CancellationToken ct = default)
            => Task.FromResult(_rows);
    }

    private static InMemoryAppDb SeedWithSupervisor()
    {
        var db = InMemoryAppDb.New();
        db.Users.Add(new AppUser
        {
            Id = 1, Username = "boss", Email = "boss@example.com", FullName = "หัวหน้าเคลม", PasswordHash = "x",
        });
        db.Roles.Add(new Role { Id = 1, Code = "CLAIM_MANAGER", NameTh = "ผู้จัดการเคลม", NameEn = "Claim Manager" });
        db.UserRoles.Add(new UserRole { UserId = 1, RoleId = 1 });
        db.RolePermissions.Add(new RolePermission { RoleId = 1, PermissionCode = Permissions.ClaimApprove });
        db.SaveChanges();
        return db;
    }

    // Filed SLA = 2 days; in status since 10 days before Day0 → breached.
    private static ClaimAgingRow Breached() =>
        new(1, "CLM-2026-000001", "POL-2026-000001", ClaimStatus.Filed, 50_000m, Day0.AddDays(-10));

    [Fact]
    public async Task Escalates_a_breached_claim_to_supervisors()
    {
        await using var db = SeedWithSupervisor();
        var sender = new FakeNotificationSender(result: true);

        var n = await ClaimEscalation.SendEscalationsAsync(
            db, new FakeAgingReader(Breached()), sender, new FixedClockProvider(Day0));

        Assert.Equal(1, n);
        var msg = Assert.Single(sender.Sent);
        Assert.Contains("boss@example.com", msg.Recipient);
        Assert.Contains("CLM-2026-000001", msg.Subject);
        var note = Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal("Sent", note.Status);
    }

    [Fact]
    public async Task Ignores_claims_within_sla()
    {
        await using var db = SeedWithSupervisor();
        var sender = new FakeNotificationSender();
        var within = new ClaimAgingRow(2, "CLM-2026-000002", "POL-2026-000002", ClaimStatus.Filed, 10_000m, Day0.AddDays(-1));

        var n = await ClaimEscalation.SendEscalationsAsync(
            db, new FakeAgingReader(within), sender, new FixedClockProvider(Day0));

        Assert.Equal(0, n);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task Falls_back_to_a_placeholder_recipient_when_no_supervisor_exists()
    {
        await using var db = InMemoryAppDb.New();   // no users seeded
        var sender = new FakeNotificationSender(result: true);

        var n = await ClaimEscalation.SendEscalationsAsync(
            db, new FakeAgingReader(Breached()), sender, new FixedClockProvider(Day0));

        Assert.Equal(1, n);
        Assert.Equal("claims-supervisors", Assert.Single(sender.Sent).Recipient);
    }

    [Fact]
    public async Task Is_idempotent_within_a_status_period()
    {
        await using var db = SeedWithSupervisor();
        var sender = new FakeNotificationSender(result: true);
        var reader = new FakeAgingReader(Breached());
        var clock = new FixedClockProvider(Day0);

        var first = await ClaimEscalation.SendEscalationsAsync(db, reader, sender, clock);
        var second = await ClaimEscalation.SendEscalationsAsync(db, reader, sender, clock);

        Assert.Equal(1, first);
        Assert.Equal(0, second);   // not re-escalated
        Assert.Single(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Re_escalates_after_the_claim_moves_to_a_new_status()
    {
        await using var db = SeedWithSupervisor();
        var sender = new FakeNotificationSender(result: true);

        // Run 1: breached in Filed at Day0; escalation recorded with CreatedAt = Day0.
        var first = await ClaimEscalation.SendEscalationsAsync(
            db, new FakeAgingReader(Breached()), sender, new FixedClockProvider(Day0));

        // Run 2 (20 days later): same claim, now Assessment since Day0+5 (after the first escalation)
        // and breaching the 5-day Assessment SLA → re-arms and escalates again.
        var later = Day0.AddDays(20);
        var moved = new ClaimAgingRow(1, "CLM-2026-000001", "POL-2026-000001", ClaimStatus.Assessment, 50_000m, Day0.AddDays(5));
        var afterMove = await ClaimEscalation.SendEscalationsAsync(
            db, new FakeAgingReader(moved), sender, new FixedClockProvider(later));

        Assert.Equal(1, first);
        Assert.Equal(1, afterMove);
        Assert.Equal(2, await db.Notifications.CountAsync());
    }
}

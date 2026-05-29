using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Infrastructure.Persistence;

namespace MotorInsurance.Infrastructure.Identity;

/// <summary>
/// Idempotent seeding of demo users. Roles/permissions are reference data owned by
/// Liquibase (V005); users carry hashed passwords so they are seeded here, where the
/// same <see cref="IPasswordHasher"/> the API verifies against is available.
/// Passwords are documented in README — change them before any non-demo use.
/// </summary>
public static class AuthDataSeeder
{
    private static readonly (string Username, string Email, string FullName, string Password, string Role)[] Seeds =
    {
        ("admin",       "admin@motor.local",       "ผู้ดูแลระบบ",           "Admin@123",   "ADMIN"),
        ("underwriter", "underwriter@motor.local", "เจ้าหน้าที่รับประกัน",   "Under@123",   "UNDERWRITER"),
        ("claims",      "claims@motor.local",      "เจ้าหน้าที่สินไหม",      "Claims@123",  "CLAIMS"),
        ("finance",     "finance@motor.local",     "เจ้าหน้าที่การเงิน",     "Finance@123", "FINANCE"),
        ("viewer",      "viewer@motor.local",      "ผู้ดูข้อมูล",            "Viewer@123",  "VIEWER"),
    };

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var roleIdByCode = await db.Roles.ToDictionaryAsync(r => r.Code, r => r.Id, ct);
        if (roleIdByCode.Count == 0) return; // Liquibase has not seeded roles yet — nothing to attach to.

        foreach (var s in Seeds)
        {
            if (!roleIdByCode.TryGetValue(s.Role, out var roleId)) continue;

            var user = new AppUser
            {
                Username = s.Username,
                Email = s.Email,
                FullName = s.FullName,
                PasswordHash = hasher.Hash(s.Password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Users.Add(user);
            db.UserRoles.Add(new UserRole { User = user, RoleId = roleId });
        }

        await db.SaveChangesAsync(ct);
    }
}

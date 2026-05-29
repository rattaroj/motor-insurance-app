using MotorInsurance.Domain.Common;

namespace MotorInsurance.Domain.Entities;

public class AppUser : BaseEntity
{
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class Role
{
    public long Id { get; set; }
    public string Code { get; set; } = default!;
    public string NameTh { get; set; } = default!;
    public string NameEn { get; set; } = default!;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Permission
{
    public string Code { get; set; } = default!;
    public string NameTh { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Category { get; set; } = default!;
}

public class UserRole
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public AppUser User { get; set; } = default!;
    public Role Role { get; set; } = default!;
}

public class RolePermission
{
    public long RoleId { get; set; }
    public string PermissionCode { get; set; } = default!;
    public Role Role { get; set; } = default!;
    public Permission Permission { get; set; } = default!;
}

public class RefreshToken : BaseEntity
{
    public long UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
    public AppUser User { get; set; } = default!;

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}

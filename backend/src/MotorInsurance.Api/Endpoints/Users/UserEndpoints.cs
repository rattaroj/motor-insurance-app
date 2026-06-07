using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Users;

public record UserDto(
    long Id, string Username, string Email, string FullName, bool IsActive,
    DateTime? LastLoginAt, IReadOnlyList<long> RoleIds, IReadOnlyList<string> Roles);

public record RoleDto(long Id, string Code, string NameTh, string NameEn);

public record CreateUserRequest(string Username, string Email, string FullName, string Password, IReadOnlyList<long> RoleIds);
public record UpdateUserRequest(string Email, string FullName, bool IsActive, IReadOnlyList<long> RoleIds);
public record ResetPasswordRequest(string Password);
public record CreateUserResponse(long Id);

public class CreateUserValidator : Validator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().Length(3, 50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(100);
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("ต้องกำหนดอย่างน้อยหนึ่งบทบาท");
    }
}

public class UpdateUserValidator : Validator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RoleIds).NotEmpty().WithMessage("ต้องกำหนดอย่างน้อยหนึ่งบทบาท");
    }
}

public class ResetPasswordValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordValidator() => RuleFor(x => x.Password).NotEmpty().MinimumLength(6).MaximumLength(100);
}

/// <summary>Shared helpers for the user-management endpoints.</summary>
internal static class UserAdminGuards
{
    public const string AdminRoleCode = "ADMIN";

    /// <summary>True when an active ADMIN user other than <paramref name="excludeUserId"/> exists.</summary>
    public static Task<bool> AnotherActiveAdminExistsAsync(IAppDbContext db, long excludeUserId, CancellationToken ct) =>
        db.Users.AnyAsync(
            u => u.Id != excludeUserId && u.IsActive && u.UserRoles.Any(ur => ur.Role.Code == AdminRoleCode), ct);

    public static async Task EnsureRolesExistAsync(IAppDbContext db, IReadOnlyList<long> roleIds, CancellationToken ct)
    {
        var found = await db.Roles.CountAsync(r => roleIds.Contains(r.Id), ct);
        if (found != roleIds.Distinct().Count())
            throw new NotFoundException(nameof(Role), string.Join(",", roleIds));
    }
}

/// <summary>GET /api/users — all users with their roles.</summary>
public class GetUsersEndpoint : EndpointWithoutRequest<IReadOnlyList<UserDto>>
{
    private readonly IAppDbContext _db;
    public GetUsersEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("users");
        Policies(PermissionPolicy.For(Perms.UserRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.Users.AsNoTracking()
            .OrderBy(u => u.Username)
            .Select(u => new UserDto(
                u.Id, u.Username, u.Email, u.FullName, u.IsActive, u.LastLoginAt,
                u.UserRoles.Select(ur => ur.RoleId).ToList(),
                u.UserRoles.Select(ur => ur.Role.NameTh).ToList()))
            .ToListAsync(ct);
}

/// <summary>GET /api/roles — assignable roles (for the user form).</summary>
public class GetRolesEndpoint : EndpointWithoutRequest<IReadOnlyList<RoleDto>>
{
    private readonly IAppDbContext _db;
    public GetRolesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("roles");
        Policies(PermissionPolicy.For(Perms.UserRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.Roles.AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new RoleDto(r.Id, r.Code, r.NameTh, r.NameEn))
            .ToListAsync(ct);
}

/// <summary>POST /api/users — create a user with hashed password + role assignments.</summary>
public class CreateUserEndpoint : Endpoint<CreateUserRequest, CreateUserResponse>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTimeProvider _clock;
    public CreateUserEndpoint(IAppDbContext db, IPasswordHasher hasher, IDateTimeProvider clock)
        => (_db, _hasher, _clock) = (db, hasher, clock);

    public override void Configure()
    {
        Post("users");
        Policies(PermissionPolicy.For(Perms.UserManage));
    }

    public override async Task HandleAsync(CreateUserRequest r, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Username == r.Username, ct))
            throw new ConflictException($"ชื่อผู้ใช้ '{r.Username}' มีอยู่แล้ว");
        if (await _db.Users.AnyAsync(u => u.Email == r.Email, ct))
            throw new ConflictException($"อีเมล '{r.Email}' มีอยู่แล้ว");
        await UserAdminGuards.EnsureRolesExistAsync(_db, r.RoleIds, ct);

        var user = new AppUser
        {
            Username = r.Username,
            Email = r.Email,
            FullName = r.FullName,
            PasswordHash = _hasher.Hash(r.Password),
            IsActive = true,
            CreatedAt = _clock.UtcNow,
            UserRoles = r.RoleIds.Distinct().Select(id => new UserRole { RoleId = id }).ToList(),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await Send.ResponseAsync(new CreateUserResponse(user.Id), 201, ct);
    }
}

/// <summary>PUT /api/users/{id} — update profile, active flag, and role assignments.</summary>
public class UpdateUserEndpoint : Endpoint<UpdateUserRequest>
{
    private readonly IAppDbContext _db;
    public UpdateUserEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("users/{id}");
        Policies(PermissionPolicy.For(Perms.UserManage));
    }

    public override async Task HandleAsync(UpdateUserRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var user = await _db.Users.Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(AppUser), id);

        if (await _db.Users.AnyAsync(u => u.Email == r.Email && u.Id != id, ct))
            throw new ConflictException($"อีเมล '{r.Email}' มีอยู่แล้ว");
        await UserAdminGuards.EnsureRolesExistAsync(_db, r.RoleIds, ct);

        // Guard: never leave the system without an active ADMIN.
        var roleIds = r.RoleIds.Distinct().ToList();
        var adminRoleId = await _db.Roles.Where(x => x.Code == UserAdminGuards.AdminRoleCode).Select(x => x.Id).FirstOrDefaultAsync(ct);
        var wouldBeActiveAdmin = r.IsActive && roleIds.Contains(adminRoleId);
        if (!wouldBeActiveAdmin && !await UserAdminGuards.AnotherActiveAdminExistsAsync(_db, id, ct))
            throw new ConflictException("ต้องมีผู้ดูแลระบบที่ใช้งานอยู่อย่างน้อยหนึ่งคน");

        user.Email = r.Email;
        user.FullName = r.FullName;
        user.IsActive = r.IsActive;

        // Replace role assignments.
        _db.UserRoles.RemoveRange(user.UserRoles);
        foreach (var rid in roleIds)
            _db.UserRoles.Add(new UserRole { UserId = id, RoleId = rid });

        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>POST /api/users/{id}/reset-password — set a new password and revoke active sessions.</summary>
public class ResetPasswordEndpoint : Endpoint<ResetPasswordRequest>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTimeProvider _clock;
    public ResetPasswordEndpoint(IAppDbContext db, IPasswordHasher hasher, IDateTimeProvider clock)
        => (_db, _hasher, _clock) = (db, hasher, clock);

    public override void Configure()
    {
        Post("users/{id}/reset-password");
        Policies(PermissionPolicy.For(Perms.UserManage));
    }

    public override async Task HandleAsync(ResetPasswordRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(AppUser), id);

        user.PasswordHash = _hasher.Hash(r.Password);

        // Force re-login: revoke the user's active refresh tokens.
        var now = _clock.UtcNow;
        await _db.RefreshTokens
            .Where(t => t.UserId == id && t.RevokedAt == null)
            .ForEachAsync(t => t.RevokedAt = now, ct);

        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/users/{id} — remove a user (cannot delete self or the last active admin).</summary>
public class DeleteUserEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    public DeleteUserEndpoint(IAppDbContext db, ICurrentUser currentUser) => (_db, _currentUser) = (db, currentUser);

    public override void Configure()
    {
        Delete("users/{id}");
        Policies(PermissionPolicy.For(Perms.UserManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        if (_currentUser.UserId == id)
            throw new ConflictException("ไม่สามารถลบบัญชีของตนเองได้");

        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(AppUser), id);

        var isActiveAdmin = user.IsActive && user.UserRoles.Any(ur => ur.Role.Code == UserAdminGuards.AdminRoleCode);
        if (isActiveAdmin && !await UserAdminGuards.AnotherActiveAdminExistsAsync(_db, id, ct))
            throw new ConflictException("ต้องมีผู้ดูแลระบบที่ใช้งานอยู่อย่างน้อยหนึ่งคน");

        _db.Users.Remove(user);  // user_role + refresh_token cascade via FK
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

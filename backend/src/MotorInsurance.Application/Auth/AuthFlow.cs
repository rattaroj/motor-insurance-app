using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Auth;

// ---------- DTOs ----------
public record UserProfileDto(
    long Id,
    string Username,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>
/// Login/refresh result. <see cref="RefreshToken"/> is the RAW token — the API layer sets it
/// as an httpOnly cookie and never returns it in the JSON body.
/// </summary>
public record AuthResultDto(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserProfileDto User);

/// <summary>
/// Authentication flow logic shared by the auth endpoints (no MediatR). Endpoints own the
/// HTTP/cookie concerns; this owns token issuance, refresh-token rotation, and profile loading.
/// </summary>
public static class AuthFlow
{
    public static async Task<AuthResultDto> LoginAsync(
        IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, IDateTimeProvider clock,
        string username, string password, CancellationToken ct)
    {
        var user = await LoadWithRoles(db).FirstOrDefaultAsync(u => u.Username == username, ct);

        // Same message whether the user is missing, inactive, or the password is wrong.
        if (user is null || !user.IsActive || !hasher.Verify(password, user.PasswordHash))
            throw new UnauthorizedException("Invalid username or password.");

        user.LastLoginAt = clock.UtcNow;
        var result = Issue(db, tokens, user);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public static async Task<AuthResultDto> RefreshAsync(
        IAppDbContext db, ITokenService tokens, IDateTimeProvider clock,
        string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            throw new UnauthorizedException("Missing refresh token.");

        var hash = tokens.HashRefreshToken(rawToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || !stored.IsUsable(clock.UtcNow))
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var user = await LoadWithRoles(db).FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var result = Issue(db, tokens, user);

        // Rotate: revoke the presented token and link it to its replacement.
        stored.RevokedAt = clock.UtcNow;
        stored.ReplacedByHash = tokens.HashRefreshToken(result.RefreshToken);

        await db.SaveChangesAsync(ct);
        return result;
    }

    public static async Task LogoutAsync(
        IAppDbContext db, ITokenService tokens, IDateTimeProvider clock,
        string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;

        var hash = tokens.HashRefreshToken(rawToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public static async Task<UserProfileDto> GetProfileAsync(IAppDbContext db, long userId, CancellationToken ct)
    {
        var user = await LoadWithRoles(db).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException(nameof(AppUser), userId);
        return BuildProfile(user);
    }

    private static AuthResultDto Issue(IAppDbContext db, ITokenService tokens, AppUser user)
    {
        var profile = BuildProfile(user);
        var access = tokens.CreateAccessToken(user, profile.Roles, profile.Permissions);
        var refresh = tokens.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.Hash,
            ExpiresAt = refresh.ExpiresAtUtc,
        });

        return new AuthResultDto(access.Token, access.ExpiresAtUtc, refresh.Raw, refresh.ExpiresAtUtc, profile);
    }

    private static UserProfileDto BuildProfile(AppUser user)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Code).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.PermissionCode))
            .Distinct().ToList();
        return new UserProfileDto(user.Id, user.Username, user.FullName, user.Email, roles, permissions);
    }

    private static IQueryable<AppUser> LoadWithRoles(IAppDbContext db) =>
        db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions);
}

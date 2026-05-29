using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Auth.Commands;

// ---------- DTOs ----------
public record UserProfileDto(
    long Id,
    string Username,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

/// <summary>
/// Login/refresh result. <see cref="RefreshToken"/> is the RAW token — the API layer
/// sets it as an httpOnly cookie and never returns it in the JSON body.
/// </summary>
public record AuthResultDto(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserProfileDto User);

// ============================================================
// Login
// ============================================================
public record LoginCommand(string Username, string Password) : IRequest<AuthResultDto>;

public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginHandler : IRequestHandler<LoginCommand, AuthResultDto>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public LoginHandler(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, IDateTimeProvider clock)
        => (_db, _hasher, _tokens, _clock) = (db, hasher, tokens, clock);

    public async Task<AuthResultDto> Handle(LoginCommand req, CancellationToken ct)
    {
        var user = await AuthQuery.LoadWithRoles(_db).FirstOrDefaultAsync(u => u.Username == req.Username, ct);

        // Same message whether the user is missing, inactive, or the password is wrong.
        if (user is null || !user.IsActive || !_hasher.Verify(req.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid username or password.");

        user.LastLoginAt = _clock.UtcNow;
        var result = await IssueAsync(_db, _tokens, user, ct);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    internal static async Task<AuthResultDto> IssueAsync(
        IAppDbContext db, ITokenService tokens, AppUser user, CancellationToken ct)
    {
        var roles = user.UserRoles.Select(ur => ur.Role.Code).Distinct().ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.PermissionCode))
            .Distinct().ToList();

        var access = tokens.CreateAccessToken(user, roles, permissions);
        var refresh = tokens.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.Hash,
            ExpiresAt = refresh.ExpiresAtUtc,
        });

        var profile = new UserProfileDto(user.Id, user.Username, user.FullName, user.Email, roles, permissions);
        return new AuthResultDto(access.Token, access.ExpiresAtUtc, refresh.Raw, refresh.ExpiresAtUtc, profile);
    }
}

// ============================================================
// Refresh (rotation)
// ============================================================
public record RefreshTokenCommand(string? RawToken) : IRequest<AuthResultDto>;

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public RefreshTokenHandler(IAppDbContext db, ITokenService tokens, IDateTimeProvider clock)
        => (_db, _tokens, _clock) = (db, tokens, clock);

    public async Task<AuthResultDto> Handle(RefreshTokenCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RawToken))
            throw new UnauthorizedException("Missing refresh token.");

        var hash = _tokens.HashRefreshToken(req.RawToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || !stored.IsActive(_clock.UtcNow))
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var user = await AuthQuery.LoadWithRoles(_db).FirstOrDefaultAsync(u => u.Id == stored.UserId, ct);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var result = await LoginHandler.IssueAsync(_db, _tokens, user, ct);

        // Rotate: revoke the presented token and link it to its replacement.
        stored.RevokedAt = _clock.UtcNow;
        stored.ReplacedByHash = _tokens.HashRefreshToken(result.RefreshToken);

        await _db.SaveChangesAsync(ct);
        return result;
    }
}

// ============================================================
// Logout (revoke the presented refresh token)
// ============================================================
public record LogoutCommand(string? RawToken) : IRequest;

public class LogoutHandler : IRequestHandler<LogoutCommand>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public LogoutHandler(IAppDbContext db, ITokenService tokens, IDateTimeProvider clock)
        => (_db, _tokens, _clock) = (db, tokens, clock);

    public async Task Handle(LogoutCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RawToken)) return;

        var hash = _tokens.HashRefreshToken(req.RawToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = _clock.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}

/// <summary>Shared include graph: user -> roles -> permissions.</summary>
internal static class AuthQuery
{
    public static IQueryable<AppUser> LoadWithRoles(IAppDbContext db) =>
        db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions);
}

using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Common.Interfaces;

/// <summary>Hashes and verifies user passwords (PBKDF2). Self-contained format, no Identity dependency.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public record AccessToken(string Token, DateTime ExpiresAtUtc);
public record GeneratedRefreshToken(string Raw, string Hash, DateTime ExpiresAtUtc);

/// <summary>Issues JWT access tokens and opaque refresh tokens.</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(AppUser user, IEnumerable<string> roles, IEnumerable<string> permissions);
    GeneratedRefreshToken GenerateRefreshToken();
    string HashRefreshToken(string raw);
}

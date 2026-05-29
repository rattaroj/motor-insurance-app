using Microsoft.AspNetCore.Http;
using MotorInsurance.Application.Auth;

namespace MotorInsurance.Api.Endpoints.Auth;

/// <summary>JSON body returned by login/refresh. The raw refresh token goes in an httpOnly cookie, never here.</summary>
public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserProfileDto User);

/// <summary>Reads/writes the httpOnly refresh-token cookie (scoped to /api/auth).</summary>
internal static class AuthCookie
{
    public const string Name = "refresh_token";
    public const string Path = "/api/auth";

    public static string? Read(HttpContext ctx) => ctx.Request.Cookies[Name];

    public static void Set(HttpContext ctx, string rawToken, DateTime expiresAtUtc) =>
        ctx.Response.Cookies.Append(Name, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps, // dev runs over http://localhost; enable Secure under https
            SameSite = SameSiteMode.Lax,
            Path = Path,
            Expires = expiresAtUtc,
        });

    public static void Delete(HttpContext ctx) =>
        ctx.Response.Cookies.Delete(Name, new CookieOptions { Path = Path });
}

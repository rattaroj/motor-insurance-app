using FastEndpoints;
using MotorInsurance.Application.Auth;
using MotorInsurance.Application.Common.Interfaces;

namespace MotorInsurance.Api.Endpoints.Auth;

/// <summary>POST /api/auth/logout — revoke the presented refresh token and clear the cookie.</summary>
public class LogoutEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public LogoutEndpoint(IAppDbContext db, ITokenService tokens, IDateTimeProvider clock)
        => (_db, _tokens, _clock) = (db, tokens, clock);

    public override void Configure()
    {
        Post("auth/logout");
        // No Policies() -> the global fallback policy requires an authenticated user.
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await AuthFlow.LogoutAsync(_db, _tokens, _clock, AuthCookie.Read(HttpContext), ct);
        AuthCookie.Delete(HttpContext);
        await Send.NoContentAsync(ct);
    }
}

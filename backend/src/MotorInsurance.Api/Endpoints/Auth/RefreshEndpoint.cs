using FastEndpoints;
using MotorInsurance.Application.Auth;
using MotorInsurance.Application.Common.Interfaces;

namespace MotorInsurance.Api.Endpoints.Auth;

/// <summary>POST /api/auth/refresh — rotate the refresh cookie and issue a new access token.</summary>
public class RefreshEndpoint : EndpointWithoutRequest<AuthResponse>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public RefreshEndpoint(IAppDbContext db, ITokenService tokens, IDateTimeProvider clock)
        => (_db, _tokens, _clock) = (db, tokens, clock);

    public override void Configure()
    {
        Post("auth/refresh");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await AuthFlow.RefreshAsync(_db, _tokens, _clock, AuthCookie.Read(HttpContext), ct);
        AuthCookie.Set(HttpContext, result.RefreshToken, result.RefreshTokenExpiresAt);
        Response = new AuthResponse(result.AccessToken, result.ExpiresAt, result.User);
    }
}

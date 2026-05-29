using FastEndpoints;
using FluentValidation;
using MotorInsurance.Application.Auth;
using MotorInsurance.Application.Common.Interfaces;

namespace MotorInsurance.Api.Endpoints.Auth;

public record LoginRequest(string Username, string Password);

public class LoginValidator : Validator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty();
    }
}

/// <summary>POST /api/auth/login — issue access token + set the httpOnly refresh cookie.</summary>
public class LoginEndpoint : Endpoint<LoginRequest, AuthResponse>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public LoginEndpoint(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, IDateTimeProvider clock)
        => (_db, _hasher, _tokens, _clock) = (db, hasher, tokens, clock);

    public override void Configure()
    {
        Post("auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest r, CancellationToken ct)
    {
        var result = await AuthFlow.LoginAsync(_db, _hasher, _tokens, _clock, r.Username, r.Password, ct);
        AuthCookie.Set(HttpContext, result.RefreshToken, result.RefreshTokenExpiresAt);
        Response = new AuthResponse(result.AccessToken, result.ExpiresAt, result.User);
    }
}

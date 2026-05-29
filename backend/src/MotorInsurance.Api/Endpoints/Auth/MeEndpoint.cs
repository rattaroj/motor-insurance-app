using FastEndpoints;
using MotorInsurance.Application.Auth;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;

namespace MotorInsurance.Api.Endpoints.Auth;

/// <summary>GET /api/auth/me — the current user's profile (roles + permissions).</summary>
public class MeEndpoint : EndpointWithoutRequest<UserProfileDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public MeEndpoint(IAppDbContext db, ICurrentUser currentUser) => (_db, _currentUser) = (db, currentUser);

    public override void Configure()
    {
        Get("auth/me");
        // No Policies() -> the global fallback policy requires an authenticated user.
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedException("Not authenticated.");
        Response = await AuthFlow.GetProfileAsync(_db, userId, ct);
    }
}

using FastEndpoints;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

/// <summary>
/// GET /api/claims/{id}/history — system-versioned (temporal) history.
/// The TemporalAll() query lives in Infrastructure (IClaimHistoryReader); this endpoint delegates.
/// </summary>
public class GetClaimHistoryEndpoint : EndpointWithoutRequest<IReadOnlyList<ClaimHistoryDto>>
{
    private readonly IClaimHistoryReader _history;
    public GetClaimHistoryEndpoint(IClaimHistoryReader history) => _history = history;

    public override void Configure()
    {
        Get("claims/{id}/history");
        Policies(PermissionPolicy.For(Perms.ClaimRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => Response = await _history.GetHistoryAsync(Route<long>("id"), ct);
}

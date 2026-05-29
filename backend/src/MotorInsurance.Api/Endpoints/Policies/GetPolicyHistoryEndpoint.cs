using FastEndpoints;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

/// <summary>
/// GET /api/policies/{id}/history — system-versioned (temporal) history.
/// The TemporalAll() query lives in Infrastructure (IPolicyHistoryReader); this endpoint delegates.
/// </summary>
public class GetPolicyHistoryEndpoint : EndpointWithoutRequest<IReadOnlyList<PolicyHistoryDto>>
{
    private readonly IPolicyHistoryReader _history;
    public GetPolicyHistoryEndpoint(IPolicyHistoryReader history) => _history = history;

    public override void Configure()
    {
        Get("policies/{id}/history");
        Policies(PermissionPolicy.For(Perms.PolicyRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
        => Response = await _history.GetHistoryAsync(Route<long>("id"), ct);
}

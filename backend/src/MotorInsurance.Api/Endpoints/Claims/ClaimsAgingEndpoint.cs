using FastEndpoints;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Claims;
using MotorInsurance.Application.Common.Interfaces;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public record ClaimAgingDto(
    long Id, string ClaimNo, string PolicyNo, string Status, decimal ClaimedAmount,
    DateTime StatusSince, int DaysInStatus, int SlaDays, bool Breached);

/// <summary>
/// GET /api/claims/aging — open claims with how long they've sat in their current status
/// (from the temporal history) and whether that breaches the per-status SLA. The claims
/// team's "what's overdue" worklist, breached items first.
/// </summary>
public class ClaimsAgingEndpoint : EndpointWithoutRequest<IReadOnlyList<ClaimAgingDto>>
{
    private readonly IClaimAgingReader _reader;
    private readonly IDateTimeProvider _clock;
    public ClaimsAgingEndpoint(IClaimAgingReader reader, IDateTimeProvider clock) => (_reader, _clock) = (reader, clock);

    public override void Configure()
    {
        Get("claims/aging");
        Policies(PermissionPolicy.For(Perms.ClaimRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var today = _clock.UtcNow.Date;
        var rows = await _reader.GetOpenAsync(ct);

        Response = rows
            .Select(r =>
            {
                var days = Math.Max(0, (today - r.StatusSince.Date).Days);
                var sla = ClaimSla.DaysFor(r.Status);
                return new ClaimAgingDto(
                    r.Id, r.ClaimNo, r.PolicyNo, r.Status.ToString(), r.ClaimedAmount,
                    r.StatusSince, days, sla, days > sla);
            })
            .OrderByDescending(x => x.Breached)
            .ThenByDescending(x => x.DaysInStatus - x.SlaDays)
            .ToList();
    }
}

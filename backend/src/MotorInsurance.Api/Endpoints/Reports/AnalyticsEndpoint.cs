using FastEndpoints;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Reports;

public record MonthPremium(string Month, decimal Premium);
public record LabelCount(string Label, int Count);

public record AnalyticsDto(
    decimal PremiumWritten,
    decimal ClaimsPaid,
    double LossRatio,
    IReadOnlyList<MonthPremium> PremiumByMonth,
    IReadOnlyList<LabelCount> PoliciesByStatus,
    IReadOnlyList<LabelCount> PoliciesByCoverage,
    IReadOnlyList<LabelCount> ClaimsByStatus);

/// <summary>GET /api/reports/analytics — aggregates for the reporting dashboard.</summary>
public class AnalyticsEndpoint : EndpointWithoutRequest<AnalyticsDto>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public AnalyticsEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("reports/analytics");
        Policies(PermissionPolicy.For(Perms.DashboardRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var from = Query<DateOnly?>("from", isRequired: false);
        var to = Query<DateOnly?>("to", isRequired: false);
        Response = await AnalyticsQuery.ComputeAsync(_db, _clock, from, to, ct);
    }
}

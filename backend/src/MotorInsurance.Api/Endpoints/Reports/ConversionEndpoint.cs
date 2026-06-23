using FastEndpoints;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Reports;

public record ConversionByCoverage(string Coverage, int Quotes, int Bound, double Rate);
public record MonthConversion(string Month, int Quotes, int Bound);

public record ConversionDto(
    int TotalQuotes,
    int BoundQuotes,
    double ConversionRate,
    int OpenQuotes,
    int ExpiredUnbound,
    decimal QuotedPremium,
    decimal BoundPremium,
    double AvgDaysToBind,
    IReadOnlyList<ConversionByCoverage> ByCoverage,
    IReadOnlyList<MonthConversion> ByMonth);

/// <summary>GET /api/reports/conversion — quote-to-bind conversion funnel for the reporting dashboard.</summary>
public class ConversionEndpoint : EndpointWithoutRequest<ConversionDto>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public ConversionEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("reports/conversion");
        Policies(PermissionPolicy.For(Perms.DashboardRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var from = Query<DateOnly?>("from", isRequired: false);
        var to = Query<DateOnly?>("to", isRequired: false);
        Response = await ConversionQuery.ComputeAsync(_db, _clock, from, to, ct);
    }
}

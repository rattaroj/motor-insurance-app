using FastEndpoints;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Common;
using MotorInsurance.Application.Common.Interfaces;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Reports;

/// <summary>
/// GET /api/reports/analytics/export?from=&amp;to= — the reporting figures as a CSV download
/// (KPIs, monthly premium, and the status/coverage breakdowns) for the same date range.
/// </summary>
public class ExportAnalyticsEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public ExportAnalyticsEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("reports/analytics/export");
        Policies(PermissionPolicy.For(Perms.DashboardRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var from = Query<DateOnly?>("from", isRequired: false);
        var to = Query<DateOnly?>("to", isRequired: false);
        var a = await AnalyticsQuery.ComputeAsync(_db, _clock, from, to, ct);

        // One flat CSV: a "section" column groups KPIs, the monthly series and the breakdowns.
        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "KPI", "เบี้ยรับรวม", Csv.Num(a.PremiumWritten) },
            new[] { "KPI", "ค่าสินไหมจ่าย", Csv.Num(a.ClaimsPaid) },
            new[] { "KPI", "Loss Ratio (%)", Csv.Num((decimal)(a.LossRatio * 100)) },
        };
        rows.AddRange(a.PremiumByMonth.Select(m => (IReadOnlyList<string?>)new[] { "เบี้ยรับรายเดือน", m.Month, Csv.Num(m.Premium) }));
        rows.AddRange(a.PoliciesByStatus.Select(s => (IReadOnlyList<string?>)new[] { "กรมธรรม์ตามสถานะ", s.Label, s.Count.ToString() }));
        rows.AddRange(a.PoliciesByCoverage.Select(s => (IReadOnlyList<string?>)new[] { "กรมธรรม์ตามชั้น", s.Label, s.Count.ToString() }));
        rows.AddRange(a.ClaimsByStatus.Select(s => (IReadOnlyList<string?>)new[] { "เคลมตามสถานะ", s.Label, s.Count.ToString() }));

        var csv = Csv.Build(new[] { "หมวด", "รายการ", "ค่า" }, rows);
        await Send.BytesAsync(csv, "analytics.csv", contentType: "text/csv", cancellation: ct);
    }
}

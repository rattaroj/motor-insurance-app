using System.Globalization;
using FastEndpoints;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Common;
using MotorInsurance.Application.Common.Interfaces;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Reports;

/// <summary>
/// GET /api/reports/conversion/export?from=&amp;to= — the quote-to-bind funnel as a CSV download
/// (KPIs, the per-coverage funnel, and the monthly quotes-vs-bound series) for the same date range.
/// </summary>
public class ExportConversionEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public ExportConversionEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("reports/conversion/export");
        Policies(PermissionPolicy.For(Perms.DashboardRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var from = Query<DateOnly?>("from", isRequired: false);
        var to = Query<DateOnly?>("to", isRequired: false);
        var c = await ConversionQuery.ComputeAsync(_db, _clock, from, to, ct);

        static string Pct(double v) => (v * 100).ToString("0.#", CultureInfo.InvariantCulture);

        var rows = new List<IReadOnlyList<string?>>
        {
            new[] { "KPI", "ใบเสนอราคาทั้งหมด", c.TotalQuotes.ToString() },
            new[] { "KPI", "ปิดการขาย (ออกกรมธรรม์)", c.BoundQuotes.ToString() },
            new[] { "KPI", "อัตราปิดการขาย (%)", Pct(c.ConversionRate) },
            new[] { "KPI", "รอตัดสินใจ (ยังไม่หมดอายุ)", c.OpenQuotes.ToString() },
            new[] { "KPI", "หมดอายุไม่ปิดการขาย", c.ExpiredUnbound.ToString() },
            new[] { "KPI", "เบี้ยที่เสนอรวม", Csv.Num(c.QuotedPremium) },
            new[] { "KPI", "เบี้ยที่ปิดได้", Csv.Num(c.BoundPremium) },
            new[] { "KPI", "เฉลี่ยวันจากเสนอถึงปิด", c.AvgDaysToBind.ToString("0.#", CultureInfo.InvariantCulture) },
        };
        rows.AddRange(c.ByCoverage.Select(x => (IReadOnlyList<string?>)new[]
            { "ตามชั้นความคุ้มครอง", x.Coverage, $"{x.Bound}/{x.Quotes} ({Pct(x.Rate)}%)" }));
        rows.AddRange(c.ByMonth.Select(m => (IReadOnlyList<string?>)new[]
            { "รายเดือน (เสนอ/ปิด)", m.Month, $"{m.Bound}/{m.Quotes}" }));

        var csv = Csv.Build(new[] { "หมวด", "รายการ", "ค่า" }, rows);
        await Send.BytesAsync(csv, "conversion.csv", contentType: "text/csv", cancellation: ct);
    }
}

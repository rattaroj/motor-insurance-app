using MotorInsurance.Application.Quotations;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorInsurance.Api.Documents;

/// <summary>Data needed to render a policy schedule (ตารางกรมธรรม์).</summary>
public record PolicyScheduleData(
    string PolicyNo, string CoverageLabel, string StatusLabel,
    string CustomerName, string NationalId, string? Phone, string? Email,
    string VehicleRegistration, string VehicleProvince, string VehicleDesc, int VehicleYear, string? ChassisNo,
    decimal SumInsured, DateOnly? EffectiveDate, DateOnly? ExpiryDate,
    int NcbPercent, decimal Deductible, PremiumBreakdown Breakdown,
    IReadOnlyList<string> Riders, IReadOnlyList<string> Drivers, DateTime GeneratedAt);

/// <summary>QuestPDF policy schedule — A4, Thai (Sarabun), shows the full premium breakdown.</summary>
public class PolicyScheduleDocument : IDocument
{
    private readonly PolicyScheduleData _d;
    public PolicyScheduleDocument(PolicyScheduleData data) => _d = data;

    private static string Baht(decimal v) => $"{v:N2} บาท";
    private static string Day(DateOnly? d) => d?.ToString("dd/MM/yyyy", PdfSetup.Culture) ?? "-";

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.DefaultTextStyle(x => x.FontFamily(PdfSetup.FontFamily).FontSize(11).LineHeight(1.3f));

        page.Header().Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("บริษัท มอเตอร์ อินชัวรันส์ จำกัด").Bold().FontSize(15);
                    c.Item().Text("ระบบประกันภัยรถยนต์").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
                r.ConstantItem(160).AlignRight().Column(c =>
                {
                    c.Item().Text("ตารางกรมธรรม์").Bold().FontSize(14);
                    c.Item().Text(_d.PolicyNo).FontSize(12).FontColor(Colors.Blue.Darken2);
                    c.Item().Text($"สถานะ: {_d.StatusLabel}").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

        page.Content().PaddingVertical(10).Column(col =>
        {
            col.Spacing(14);

            Section(col, "ผู้เอาประกันภัย", t =>
            {
                LabelValue(t, "ชื่อ-นามสกุล", _d.CustomerName);
                LabelValue(t, "เลขบัตรประชาชน", _d.NationalId);
                LabelValue(t, "โทรศัพท์", _d.Phone ?? "-");
                LabelValue(t, "อีเมล", _d.Email ?? "-");
            });

            Section(col, "รถยนต์ที่เอาประกัน", t =>
            {
                LabelValue(t, "ทะเบียน", $"{_d.VehicleRegistration} ({_d.VehicleProvince})");
                LabelValue(t, "รถยนต์", $"{_d.VehicleDesc} ปี {_d.VehicleYear}");
                LabelValue(t, "เลขตัวถัง", _d.ChassisNo ?? "-");
            });

            Section(col, "ความคุ้มครอง", t =>
            {
                LabelValue(t, "ประเภท", _d.CoverageLabel);
                LabelValue(t, "ทุนประกัน", Baht(_d.SumInsured));
                LabelValue(t, "ระยะคุ้มครอง", $"{Day(_d.EffectiveDate)} ถึง {Day(_d.ExpiryDate)}");
                LabelValue(t, "ความคุ้มครองเสริม", _d.Riders.Count > 0 ? string.Join(", ", _d.Riders) : "ไม่มี");
            });

            col.Item().Element(PremiumBreakdownBox);

            if (_d.Drivers.Count > 0)
                Section(col, "ผู้ขับขี่ระบุชื่อ", t =>
                {
                    for (var i = 0; i < _d.Drivers.Count; i++)
                        LabelValue(t, $"คนที่ {i + 1}", _d.Drivers[i]);
                });
        });

        page.Footer().Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text($"ออกเอกสารเมื่อ {_d.GeneratedAt.ToString("dd/MM/yyyy HH:mm", PdfSetup.Culture)} น. — เอกสารนี้พิมพ์จากระบบ")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
                r.ConstantItem(80).AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                    t.Span("หน้า ");
                    t.CurrentPageNumber();
                    t.Span("/");
                    t.TotalPages();
                });
            });
        });
    });

    private void PremiumBreakdownBox(IContainer container) => container
        .Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4)
        .Padding(10).Column(col =>
        {
            col.Item().Text("เบี้ยประกัน").Bold().FontSize(12);
            col.Item().PaddingTop(4).Column(rows =>
            {
                var b = _d.Breakdown;
                MoneyRow(rows, "เบี้ยฐาน", b.BasePremium, false);
                if (b.VehicleAgeLoading > 0) MoneyRow(rows, "โหลดตามอายุรถ", b.VehicleAgeLoading, false, "+");
                if (b.NcbDiscount > 0) MoneyRow(rows, $"ส่วนลดประวัติดี (NCB {_d.NcbPercent}%)", b.NcbDiscount, true, "−");
                if (b.DeductibleDiscount > 0) MoneyRow(rows, $"ส่วนลดค่าเสียหายส่วนแรก ({Baht(_d.Deductible)})", b.DeductibleDiscount, true, "−");
                if (b.RidersTotal > 0) MoneyRow(rows, "ความคุ้มครองเสริม", b.RidersTotal, false, "+");
                rows.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                rows.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text("เบี้ยสุทธิ").Bold().FontSize(12);
                    r.ConstantItem(160).AlignRight().Text(Baht(b.NetPremium)).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                });
            });
        });

    private static void MoneyRow(ColumnDescriptor col, string label, decimal value, bool discount, string sign = "") =>
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label);
            r.ConstantItem(160).AlignRight().Text($"{sign}{value:N2}")
                .FontColor(discount ? Colors.Green.Darken1 : Colors.Black);
        });

    private static void Section(ColumnDescriptor col, string title, Action<TableDescriptor> rows) =>
        col.Item().Column(c =>
        {
            c.Item().Text(title).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
            c.Item().PaddingTop(2).Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.ConstantColumn(140); cd.RelativeColumn(); });
                rows(t);
            });
        });

    private static void LabelValue(TableDescriptor t, string label, string value)
    {
        t.Cell().PaddingVertical(1).Text(label).FontColor(Colors.Grey.Darken1);
        t.Cell().PaddingVertical(1).Text(value);
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
}

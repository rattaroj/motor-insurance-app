using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorInsurance.Api.Documents;

/// <summary>One coverage option's rated breakdown, for the comparison sheet.</summary>
public record CoverageComparisonOption(
    string CoverageLabel, decimal BasePremium, decimal VehicleAgeLoading,
    decimal NcbDiscount, decimal DeductibleDiscount, decimal RidersTotal, decimal NetPremium);

/// <summary>Data for the side-by-side coverage comparison sheet (เปรียบเทียบความคุ้มครอง).</summary>
public record CoverageComparisonData(
    string VehicleRegistration, decimal SumInsured, int NcbPercent, decimal Deductible,
    IReadOnlyList<CoverageComparisonOption> Options, DateTime GeneratedAt);

/// <summary>QuestPDF coverage comparison — A4 landscape, Thai (Sarabun), one column per coverage type.</summary>
public class CoverageComparisonDocument : IDocument
{
    private readonly CoverageComparisonData _d;
    public CoverageComparisonDocument(CoverageComparisonData data) => _d = data;

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(36);
        page.DefaultTextStyle(x => x.FontFamily(PdfSetup.FontFamily).FontSize(11).LineHeight(1.3f));

        page.Header().Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("บริษัท มอเตอร์ อินชัวรันส์ จำกัด").Bold().FontSize(13);
                    c.Item().Text("ใบเปรียบเทียบความคุ้มครอง").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                r.ConstantItem(220).AlignRight().Column(c =>
                {
                    c.Item().Text($"ทะเบียนรถ {_d.VehicleRegistration}").Bold().FontSize(12);
                    c.Item().Text($"ทุนประกัน {_d.SumInsured:N0} บาท").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

        page.Content().PaddingVertical(12).Column(col =>
        {
            col.Item().PaddingBottom(8).Text($"ส่วนลดประวัติดี (NCB) {_d.NcbPercent}% · ค่าเสียหายส่วนแรก {_d.Deductible:N0} บาท")
                .FontSize(10).FontColor(Colors.Grey.Darken1);

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(1.4f);                 // row label
                    foreach (var _ in _d.Options) cd.RelativeColumn();
                });

                // Header row: coverage labels.
                t.Cell().Element(HeaderCell).Text("รายการ").Bold();
                foreach (var o in _d.Options)
                    t.Cell().Element(HeaderCell).AlignRight().Text(o.CoverageLabel).Bold();

                AmountRow(t, "เบี้ยฐาน", o => o.BasePremium);
                AmountRow(t, "โหลดอายุรถ", o => o.VehicleAgeLoading);
                AmountRow(t, "− ส่วนลด NCB", o => -o.NcbDiscount);
                AmountRow(t, "− ส่วนลดค่าเสียหายส่วนแรก", o => -o.DeductibleDiscount);
                AmountRow(t, "+ ความคุ้มครองเสริม", o => o.RidersTotal);

                // Net premium row (emphasised).
                t.Cell().Element(NetCell).Text("เบี้ยสุทธิ").Bold();
                foreach (var o in _d.Options)
                    t.Cell().Element(NetCell).AlignRight().Text($"{o.NetPremium:N2}")
                        .Bold().FontColor(Colors.Blue.Darken2);
            });
        });

        page.Footer().Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4)
                .Text($"ออกเอกสารเมื่อ {_d.GeneratedAt.ToString("dd/MM/yyyy HH:mm", PdfSetup.Culture)} น. — อัตราเบี้ยเป็นค่าประมาณการ มิใช่ข้อผูกพัน")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    });

    private void AmountRow(TableDescriptor t, string label, Func<CoverageComparisonOption, decimal> value)
    {
        t.Cell().Element(BodyCell).Text(label).FontColor(Colors.Grey.Darken1);
        foreach (var o in _d.Options)
            t.Cell().Element(BodyCell).AlignRight().Text($"{value(o):N2}");
    }

    private static IContainer HeaderCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(6);

    private static IContainer BodyCell(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(6);

    private static IContainer NetCell(IContainer c) =>
        c.Background(Colors.Grey.Lighten4).BorderTop(1).BorderColor(Colors.Grey.Lighten1).Padding(6);

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
}

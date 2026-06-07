using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorInsurance.Api.Documents;

/// <summary>Data needed to render a quotation (ใบเสนอราคา) with its premium breakdown.</summary>
public record QuotationDocData(
    string QuotationNo, string CustomerName, string VehicleRegistration, string CoverageLabel,
    decimal SumInsured, int NcbPercent, decimal Deductible, IReadOnlyList<string> Riders,
    decimal BasePremium, decimal VehicleAgeLoading, decimal NcbDiscount, decimal DeductibleDiscount,
    decimal RidersTotal, decimal NetPremium, DateOnly ValidUntil, DateTime GeneratedAt);

/// <summary>QuestPDF quotation — A4, Thai (Sarabun), with the layered premium breakdown.</summary>
public class QuotationDocument : IDocument
{
    private readonly QuotationDocData _d;
    public QuotationDocument(QuotationDocData data) => _d = data;

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.DefaultTextStyle(x => x.FontFamily(PdfSetup.FontFamily).FontSize(11).LineHeight(1.3f));

        page.Header().Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("บริษัท มอเตอร์ อินชัวรันส์ จำกัด").Bold().FontSize(14);
                    c.Item().Text("ใบเสนอราคาประกันภัยรถยนต์").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                r.ConstantItem(170).AlignRight().Column(c =>
                {
                    c.Item().Text("ใบเสนอราคา").Bold().FontSize(14);
                    c.Item().Text(_d.QuotationNo).FontSize(11).FontColor(Colors.Blue.Darken2);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

        page.Content().PaddingVertical(14).Column(col =>
        {
            col.Spacing(10);

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.ConstantColumn(120); cd.RelativeColumn(); });
                InfoRow(t, "ผู้เอาประกัน", _d.CustomerName);
                InfoRow(t, "ทะเบียนรถ", _d.VehicleRegistration);
                InfoRow(t, "ความคุ้มครอง", _d.CoverageLabel);
                InfoRow(t, "ทุนประกัน", $"{_d.SumInsured:N0} บาท");
                InfoRow(t, "ส่วนลดประวัติดี", $"{_d.NcbPercent}%");
                InfoRow(t, "ค่าเสียหายส่วนแรก", _d.Deductible > 0 ? $"{_d.Deductible:N0} บาท" : "ไม่มี");
                InfoRow(t, "ความคุ้มครองเสริม", _d.Riders.Count > 0 ? string.Join(", ", _d.Riders) : "ไม่มี");
            });

            col.Item().Text("รายละเอียดเบี้ยประกัน").Bold().FontSize(12);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.RelativeColumn(); cd.ConstantColumn(140); });
                AmountRow(t, "เบี้ยฐาน", _d.BasePremium);
                if (_d.VehicleAgeLoading > 0) AmountRow(t, "โหลดตามอายุรถ", _d.VehicleAgeLoading);
                if (_d.NcbDiscount > 0) AmountRow(t, "− ส่วนลดประวัติดี (NCB)", -_d.NcbDiscount);
                if (_d.DeductibleDiscount > 0) AmountRow(t, "− ส่วนลดค่าเสียหายส่วนแรก", -_d.DeductibleDiscount);
                if (_d.RidersTotal > 0) AmountRow(t, "+ ความคุ้มครองเสริม", _d.RidersTotal);
            });

            col.Item().PaddingTop(2).Border(1).BorderColor(Colors.Grey.Lighten1)
                .Background(Colors.Grey.Lighten4).Padding(10).Row(r =>
                {
                    r.RelativeItem().Text("เบี้ยสุทธิ").Bold().FontSize(13);
                    r.ConstantItem(160).AlignRight().Text($"{_d.NetPremium:N2} บาท")
                        .Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                });

            col.Item().Text($"ใบเสนอราคานี้มีผลถึงวันที่ {_d.ValidUntil.ToString("dd/MM/yyyy", PdfSetup.Culture)}")
                .FontSize(10).FontColor(Colors.Grey.Darken1);
        });

        page.Footer().Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4)
                .Text($"ออกเอกสารเมื่อ {_d.GeneratedAt.ToString("dd/MM/yyyy HH:mm", PdfSetup.Culture)} น. — อัตราเบี้ยเป็นค่าประมาณการ มิใช่ข้อผูกพัน")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    });

    private static void InfoRow(TableDescriptor t, string label, string value)
    {
        t.Cell().PaddingVertical(2).Text(label).FontColor(Colors.Grey.Darken1);
        t.Cell().PaddingVertical(2).Text(value);
    }

    private static void AmountRow(TableDescriptor t, string label, decimal value)
    {
        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4)
            .Text(label).FontColor(Colors.Grey.Darken1);
        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4)
            .AlignRight().Text($"{value:N2}");
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
}

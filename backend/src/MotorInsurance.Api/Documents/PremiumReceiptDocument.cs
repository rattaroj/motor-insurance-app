using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorInsurance.Api.Documents;

/// <summary>Data needed to render a premium-payment receipt (ใบเสร็จรับเงิน).</summary>
public record PremiumReceiptData(
    string PaymentNo, string PolicyNo, string CustomerName,
    decimal Amount, DateTime? PaidAt, string? ReferenceNo, DateTime GeneratedAt);

/// <summary>QuestPDF premium receipt — A5, Thai (Sarabun).</summary>
public class PremiumReceiptDocument : IDocument
{
    private readonly PremiumReceiptData _d;
    public PremiumReceiptDocument(PremiumReceiptData data) => _d = data;

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A5);
        page.Margin(36);
        page.DefaultTextStyle(x => x.FontFamily(PdfSetup.FontFamily).FontSize(11).LineHeight(1.3f));

        page.Header().Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("บริษัท มอเตอร์ อินชัวรันส์ จำกัด").Bold().FontSize(13);
                    c.Item().Text("ระบบประกันภัยรถยนต์").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                r.ConstantItem(150).AlignRight().Column(c =>
                {
                    c.Item().Text("ใบเสร็จรับเงิน").Bold().FontSize(13);
                    c.Item().Text(_d.PaymentNo).FontSize(11).FontColor(Colors.Blue.Darken2);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

        page.Content().PaddingVertical(12).Column(col =>
        {
            col.Spacing(6);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.ConstantColumn(120); cd.RelativeColumn(); });
                Row(t, "ได้รับเงินจาก", _d.CustomerName);
                Row(t, "กรมธรรม์เลขที่", _d.PolicyNo);
                Row(t, "วันที่ชำระ", _d.PaidAt?.ToString("dd/MM/yyyy HH:mm", PdfSetup.Culture) ?? "-");
                Row(t, "เลขอ้างอิง", _d.ReferenceNo ?? "-");
                Row(t, "รายการ", "เบี้ยประกันภัยรถยนต์");
            });

            col.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1)
                .Background(Colors.Grey.Lighten4).Padding(10).Row(r =>
                {
                    r.RelativeItem().Text("จำนวนเงินที่ชำระ").Bold().FontSize(12);
                    r.ConstantItem(150).AlignRight().Text($"{_d.Amount:N2} บาท")
                        .Bold().FontSize(13).FontColor(Colors.Blue.Darken2);
                });
        });

        page.Footer().Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4).Text($"ออกเอกสารเมื่อ {_d.GeneratedAt.ToString("dd/MM/yyyy HH:mm", PdfSetup.Culture)} น. — เอกสารนี้พิมพ์จากระบบ")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    });

    private static void Row(TableDescriptor t, string label, string value)
    {
        t.Cell().PaddingVertical(2).Text(label).FontColor(Colors.Grey.Darken1);
        t.Cell().PaddingVertical(2).Text(value);
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
}

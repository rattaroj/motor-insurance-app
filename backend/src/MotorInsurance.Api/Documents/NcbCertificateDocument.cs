using MotorInsurance.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorInsurance.Api.Documents;

/// <summary>Data needed to render a No-Claim-Bonus certificate (หนังสือรับรองประวัติดี).</summary>
public record NcbCertificateData(
    string PolicyNo, string CustomerName, string NationalId,
    string VehicleRegistration, string VehicleProvince,
    CoverageType Coverage, DateOnly? EffectiveDate, DateOnly? ExpiryDate,
    int NcbPercent, bool ClaimFree, DateTime GeneratedAt);

/// <summary>QuestPDF No-Claim-Bonus certificate — A4, Thai (Sarabun). Customers use it to carry their
/// claim-free history to another insurer.</summary>
public class NcbCertificateDocument : IDocument
{
    private readonly NcbCertificateData _d;
    public NcbCertificateDocument(NcbCertificateData data) => _d = data;

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(48);
        page.DefaultTextStyle(x => x.FontFamily(PdfSetup.FontFamily).FontSize(12).LineHeight(1.4f));

        page.Header().Column(col =>
        {
            col.Item().AlignCenter().Text("บริษัท มอเตอร์ อินชัวรันส์ จำกัด").Bold().FontSize(15);
            col.Item().AlignCenter().Text("หนังสือรับรองประวัติดี (No-Claim Bonus)").FontSize(13).FontColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

        page.Content().PaddingVertical(18).Column(col =>
        {
            col.Spacing(10);

            col.Item().Text(t =>
            {
                t.Span("บริษัทขอรับรองว่า ");
                t.Span(_d.CustomerName).Bold();
                t.Span($" (เลขประจำตัวประชาชน {_d.NationalId}) เป็นผู้เอาประกันภัยตามกรมธรรม์เลขที่ ");
                t.Span(_d.PolicyNo).Bold();
            });

            col.Item().Table(tbl =>
            {
                tbl.ColumnsDefinition(cd => { cd.ConstantColumn(160); cd.RelativeColumn(); });
                Row(tbl, "ทะเบียนรถ", $"{_d.VehicleRegistration} {_d.VehicleProvince}".Trim());
                Row(tbl, "ประเภทความคุ้มครอง", ThaiLabels.Coverage(_d.Coverage));
                Row(tbl, "ระยะเวลาคุ้มครอง", $"{Date(_d.EffectiveDate)} ถึง {Date(_d.ExpiryDate)}");
                Row(tbl, "ส่วนลดประวัติดี (NCB)", $"{_d.NcbPercent}%");
                Row(tbl, "ประวัติการเรียกร้อง", _d.ClaimFree ? "ไม่มีการเรียกร้องค่าสินไหม (ปลอดเคลม)" : "มีการเรียกร้องค่าสินไหมในรอบกรมธรรม์");
            });

            col.Item().PaddingTop(6).Text(_d.ClaimFree
                    ? "ในรอบระยะเวลาประกันภัยข้างต้น ผู้เอาประกันภัยไม่มีการเรียกร้องค่าสินไหมทดแทน จึงมีสิทธิได้รับส่วนลดประวัติดีตามอัตราข้างต้น"
                    : "ในรอบระยะเวลาประกันภัยข้างต้น มีการเรียกร้องค่าสินไหมทดแทน ส่วนลดประวัติดีจึงเป็นไปตามเงื่อนไขของบริษัท")
                .FontColor(Colors.Grey.Darken2);

            col.Item().PaddingTop(36).AlignRight().Column(c =>
            {
                c.Item().Text("....................................................");
                c.Item().AlignRight().Text("เจ้าหน้าที่ผู้มีอำนาจลงนาม").FontSize(10).FontColor(Colors.Grey.Darken1);
            });
        });

        page.Footer().Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4)
                .Text($"ออกเอกสารเมื่อ {_d.GeneratedAt.ToString("dd/MM/yyyy HH:mm", PdfSetup.Culture)} น. — เอกสารนี้พิมพ์จากระบบ")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    });

    private static string Date(DateOnly? d) => d?.ToString("dd/MM/yyyy", PdfSetup.Culture) ?? "-";

    private static void Row(TableDescriptor t, string label, string value)
    {
        t.Cell().PaddingVertical(3).Text(label).FontColor(Colors.Grey.Darken1);
        t.Cell().PaddingVertical(3).Text(value);
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
}

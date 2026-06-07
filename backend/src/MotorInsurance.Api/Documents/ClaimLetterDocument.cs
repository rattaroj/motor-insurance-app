using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorInsurance.Api.Documents;

/// <summary>Data for the claim settlement letter (จดหมายแจ้งผลการพิจารณาสินไหม).</summary>
public record ClaimLetterData(
    string ClaimNo, string PolicyNo, string CustomerName, string StatusLabel,
    DateOnly IncidentDate, decimal ClaimedAmount, decimal? ApprovedAmount, string? RejectReason,
    string? GarageName, string? SurveyorName, bool IsApproved, DateTime GeneratedAt);

/// <summary>QuestPDF claim result letter — A4, Thai (Sarabun).</summary>
public class ClaimLetterDocument : IDocument
{
    private readonly ClaimLetterData _d;
    public ClaimLetterDocument(ClaimLetterData data) => _d = data;

    public void Compose(IDocumentContainer container) => container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.DefaultTextStyle(x => x.FontFamily(PdfSetup.FontFamily).FontSize(11).LineHeight(1.4f));

        page.Header().Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(c =>
                {
                    c.Item().Text("บริษัท มอเตอร์ อินชัวรันส์ จำกัด").Bold().FontSize(14);
                    c.Item().Text("จดหมายแจ้งผลการพิจารณาสินไหม").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                r.ConstantItem(170).AlignRight().Column(c =>
                {
                    c.Item().Text("เลขรับแจ้งเคลม").FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(_d.ClaimNo).Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

        page.Content().PaddingVertical(16).Column(col =>
        {
            col.Spacing(10);

            col.Item().Text($"เรียน คุณ{_d.CustomerName}");
            col.Item().Text(text =>
            {
                text.Span("ตามที่ท่านได้แจ้งเคลมภายใต้กรมธรรม์เลขที่ ");
                text.Span(_d.PolicyNo).Bold();
                text.Span($" กรณีเกิดเหตุเมื่อวันที่ {_d.IncidentDate.ToString("dd/MM/yyyy", PdfSetup.Culture)} นั้น ");
                text.Span("บริษัทขอแจ้งผลการพิจารณาดังนี้");
            });

            col.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(12).Column(c =>
            {
                c.Spacing(4);
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text("ผลการพิจารณา").FontColor(Colors.Grey.Darken1);
                    r.ConstantItem(220).AlignRight().Text(_d.StatusLabel).Bold()
                        .FontColor(_d.IsApproved ? Colors.Green.Darken2 : Colors.Red.Darken2);
                });
                c.Item().Row(r =>
                {
                    r.RelativeItem().Text("จำนวนที่เรียกร้อง").FontColor(Colors.Grey.Darken1);
                    r.ConstantItem(220).AlignRight().Text($"{_d.ClaimedAmount:N2} บาท");
                });
                if (_d.IsApproved)
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("จำนวนที่อนุมัติ").Bold();
                        r.ConstantItem(220).AlignRight().Text($"{_d.ApprovedAmount ?? 0:N2} บาท")
                            .Bold().FontColor(Colors.Green.Darken2);
                    });
                if (!string.IsNullOrWhiteSpace(_d.RejectReason))
                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("เหตุผล").FontColor(Colors.Grey.Darken1);
                        r.ConstantItem(220).AlignRight().Text(_d.RejectReason);
                    });
            });

            if (!string.IsNullOrWhiteSpace(_d.GarageName) || !string.IsNullOrWhiteSpace(_d.SurveyorName))
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.ConstantColumn(120); cd.RelativeColumn(); });
                    if (!string.IsNullOrWhiteSpace(_d.GarageName))
                    {
                        t.Cell().PaddingVertical(2).Text("อู่/ศูนย์ซ่อม").FontColor(Colors.Grey.Darken1);
                        t.Cell().PaddingVertical(2).Text(_d.GarageName!);
                    }
                    if (!string.IsNullOrWhiteSpace(_d.SurveyorName))
                    {
                        t.Cell().PaddingVertical(2).Text("ผู้สำรวจภัย").FontColor(Colors.Grey.Darken1);
                        t.Cell().PaddingVertical(2).Text(_d.SurveyorName!);
                    }
                });

            col.Item().PaddingTop(8).Text(_d.IsApproved
                ? "บริษัทจะดำเนินการจ่ายค่าสินไหมทดแทนตามจำนวนที่อนุมัติ หากมีข้อสงสัยกรุณาติดต่อเจ้าหน้าที่"
                : "หากท่านมีข้อมูลเพิ่มเติมหรือประสงค์อุทธรณ์ผลการพิจารณา กรุณาติดต่อเจ้าหน้าที่");
            col.Item().PaddingTop(12).Text("ขอแสดงความนับถือ");
            col.Item().Text("ฝ่ายสินไหมทดแทน");
        });

        page.Footer().Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
            col.Item().PaddingTop(4)
                .Text($"ออกเอกสารเมื่อ {_d.GeneratedAt.ToString("dd/MM/yyyy HH:mm", PdfSetup.Culture)} น. — เอกสารนี้พิมพ์จากระบบ")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    });

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
}

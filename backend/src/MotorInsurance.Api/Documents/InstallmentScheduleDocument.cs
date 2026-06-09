using MotorInsurance.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorInsurance.Api.Documents;

/// <summary>One installment line on the schedule.</summary>
public record InstallmentRow(int Seq, DateOnly? DueDate, decimal Amount, PaymentStatus Status, DateTime? PaidAt);

/// <summary>Data for the installment-schedule document, with paid/remaining totals derived from the rows.</summary>
public record InstallmentScheduleData(
    string PolicyNo, string CustomerName,
    decimal TotalPremium, decimal Fee, int Installments, int FrequencyDays, InstallmentPlanStatus PlanStatus,
    IReadOnlyList<InstallmentRow> Rows, decimal PaidTotal, decimal RemainingTotal, DateTime GeneratedAt)
{
    /// <summary>Build the schedule, ordering rows by sequence and summing paid vs. outstanding amounts.</summary>
    public static InstallmentScheduleData Build(
        string policyNo, string customerName,
        decimal totalPremium, decimal fee, int installments, int frequencyDays, InstallmentPlanStatus planStatus,
        IReadOnlyList<InstallmentRow> rows, DateTime generatedAt)
    {
        var ordered = rows.OrderBy(r => r.Seq).ToList();
        var paid = ordered.Where(r => r.Status == PaymentStatus.Paid).Sum(r => r.Amount);
        var remaining = ordered.Where(r => r.Status != PaymentStatus.Paid).Sum(r => r.Amount);
        return new InstallmentScheduleData(
            policyNo, customerName, totalPremium, fee, installments, frequencyDays, planStatus,
            ordered, paid, remaining, generatedAt);
    }
}

/// <summary>QuestPDF installment-schedule document — A4, Thai (Sarabun).</summary>
public class InstallmentScheduleDocument : IDocument
{
    private readonly InstallmentScheduleData _d;
    public InstallmentScheduleDocument(InstallmentScheduleData data) => _d = data;

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
                    c.Item().Text("บริษัท มอเตอร์ อินชัวรันส์ จำกัด").Bold().FontSize(13);
                    c.Item().Text("ตารางผ่อนชำระเบี้ยประกันภัย").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
                r.ConstantItem(180).AlignRight().Column(c =>
                {
                    c.Item().Text("กรมธรรม์เลขที่").FontSize(9).FontColor(Colors.Grey.Darken1);
                    c.Item().Text(_d.PolicyNo).Bold().FontColor(Colors.Blue.Darken2);
                });
            });
            col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

        page.Content().PaddingVertical(12).Column(col =>
        {
            col.Spacing(8);

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd => { cd.ConstantColumn(130); cd.RelativeColumn(); });
                Kv(t, "ผู้เอาประกันภัย", _d.CustomerName);
                Kv(t, "จำนวนงวด", $"{_d.Installments} งวด (ทุก {_d.FrequencyDays} วัน)");
                Kv(t, "เบี้ยรวม", $"{_d.TotalPremium.ToString("N2", PdfSetup.Culture)} บาท");
                Kv(t, "ค่าธรรมเนียมผ่อนชำระ", $"{_d.Fee.ToString("N2", PdfSetup.Culture)} บาท");
                Kv(t, "สถานะแผน", PlanStatus(_d.PlanStatus));
            });

            col.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(50);    // งวด
                    cd.RelativeColumn();       // ครบกำหนด
                    cd.RelativeColumn();       // จำนวน
                    cd.RelativeColumn();       // สถานะ
                    cd.RelativeColumn();       // วันที่ชำระ
                });

                t.Header(h =>
                {
                    void HeaderCell(string text) =>
                        h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(text).Bold().FontSize(10);
                    HeaderCell("งวด");
                    HeaderCell("ครบกำหนด");
                    HeaderCell("จำนวน (บาท)");
                    HeaderCell("สถานะ");
                    HeaderCell("วันที่ชำระ");
                });

                foreach (var row in _d.Rows)
                {
                    BodyCell(t, row.Seq.ToString());
                    BodyCell(t, row.DueDate?.ToString("dd/MM/yyyy", PdfSetup.Culture) ?? "-");
                    BodyCell(t, row.Amount.ToString("N2", PdfSetup.Culture));
                    BodyCell(t, PaymentStatusLabel(row.Status));
                    BodyCell(t, row.PaidAt?.ToString("dd/MM/yyyy", PdfSetup.Culture) ?? "-");
                }
            });

            col.Item().PaddingTop(8).Row(r =>
            {
                r.RelativeItem();
                r.ConstantItem(220).Column(c =>
                {
                    c.Item().Row(x =>
                    {
                        x.RelativeItem().Text("ชำระแล้ว").FontColor(Colors.Grey.Darken1);
                        x.ConstantItem(110).AlignRight().Text($"{_d.PaidTotal.ToString("N2", PdfSetup.Culture)} บาท");
                    });
                    c.Item().Row(x =>
                    {
                        x.RelativeItem().Text("คงเหลือ").Bold();
                        x.ConstantItem(110).AlignRight().Text($"{_d.RemainingTotal.ToString("N2", PdfSetup.Culture)} บาท")
                            .Bold().FontColor(Colors.Blue.Darken2);
                    });
                });
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

    private static string PlanStatus(InstallmentPlanStatus s) => s switch
    {
        InstallmentPlanStatus.Active => "กำลังผ่อนชำระ",
        InstallmentPlanStatus.Completed => "ชำระครบแล้ว",
        InstallmentPlanStatus.Defaulted => "ผิดนัดชำระ",
        _ => s.ToString(),
    };

    private static string PaymentStatusLabel(PaymentStatus s) => s switch
    {
        PaymentStatus.Pending => "รอชำระ",
        PaymentStatus.Paid => "ชำระแล้ว",
        PaymentStatus.Failed => "ไม่สำเร็จ",
        PaymentStatus.Refunded => "คืนเงิน",
        _ => s.ToString(),
    };

    private static void Kv(TableDescriptor t, string label, string value)
    {
        t.Cell().PaddingVertical(2).Text(label).FontColor(Colors.Grey.Darken1);
        t.Cell().PaddingVertical(2).Text(value);
    }

    private static void BodyCell(TableDescriptor t, string text) =>
        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(text);

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
}

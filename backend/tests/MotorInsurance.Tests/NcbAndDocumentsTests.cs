using MotorInsurance.Api.Documents;
using MotorInsurance.Application.Policies;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using QuestPDF.Fluent;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// Installment-schedule builder + No-Claim-Bonus rule, plus smoke tests that the new QuestPDF
/// documents actually render (catches layout errors the compiler can't). (F3/F4)
/// </summary>
public class NcbAndDocumentsTests
{
    // QuestPDF needs its license + the embedded Thai font registered once before any GeneratePdf().
    static NcbAndDocumentsTests() => PdfSetup.Configure();

    private static readonly DateTime Gen = new(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);

    // ---- #3 installment schedule ----

    [Fact]
    public void Build_orders_rows_by_seq_and_totals_paid_vs_remaining()
    {
        var rows = new[]
        {
            new InstallmentRow(3, new DateOnly(2026, 8, 1), 3_000m, PaymentStatus.Pending, null),
            new InstallmentRow(1, new DateOnly(2026, 6, 1), 3_300m, PaymentStatus.Paid, new DateTime(2026, 6, 1)),
            new InstallmentRow(2, new DateOnly(2026, 7, 1), 3_000m, PaymentStatus.Pending, null),
        };

        var d = InstallmentScheduleData.Build(
            "POL-2026-000001", "สมชาย ใจดี", totalPremium: 9_000m, fee: 300m,
            installments: 3, frequencyDays: 30, InstallmentPlanStatus.Active, rows, Gen);

        Assert.Equal(new[] { 1, 2, 3 }, d.Rows.Select(r => r.Seq));   // ordered
        Assert.Equal(3_300m, d.PaidTotal);
        Assert.Equal(6_000m, d.RemainingTotal);
    }

    [Fact]
    public void Installment_schedule_pdf_renders_non_empty()
    {
        var rows = new[] { new InstallmentRow(1, new DateOnly(2026, 6, 1), 3_300m, PaymentStatus.Paid, Gen) };
        var d = InstallmentScheduleData.Build(
            "POL-2026-000001", "สมชาย ใจดี", 9_000m, 300m, 3, 30, InstallmentPlanStatus.Active, rows, Gen);

        var pdf = new InstallmentScheduleDocument(d).GeneratePdf();

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
    }

    // ---- #4 no-claim-bonus ----

    private static InMemoryAppDb DbWithPolicy(params (ClaimStatus Status, int Id)[] claims)
    {
        var db = InMemoryAppDb.New();
        db.Policies.Add(new Policy { Id = 1, PolicyNo = "POL-1", CustomerId = 1, VehicleId = 1 });
        foreach (var (status, id) in claims)
            db.Claims.Add(new Claim
            {
                Id = id,
                ClaimNo = $"CLM-{id}",
                PolicyId = 1,
                Status = status,
                IncidentDate = new DateOnly(2026, 1, 1),
                ClaimedAmount = 1_000m,
            });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task ClaimFree_true_when_no_claims()
    {
        await using var db = DbWithPolicy();
        Assert.True(await NoClaimBonus.IsClaimFreeAsync(db, 1, default));
    }

    [Fact]
    public async Task ClaimFree_true_when_only_rejected_claims()
    {
        await using var db = DbWithPolicy((ClaimStatus.Rejected, 1));
        Assert.True(await NoClaimBonus.IsClaimFreeAsync(db, 1, default));
    }

    [Fact]
    public async Task ClaimFree_false_when_a_non_rejected_claim_exists()
    {
        await using var db = DbWithPolicy((ClaimStatus.Rejected, 1), (ClaimStatus.Paid, 2));
        Assert.False(await NoClaimBonus.IsClaimFreeAsync(db, 1, default));
    }

    [Fact]
    public void Ncb_certificate_pdf_renders_non_empty()
    {
        var data = new NcbCertificateData(
            "POL-2026-000001", "สมชาย ใจดี", "1100000000001",
            "1กก1234", "กรุงเทพมหานคร", CoverageType.Type1,
            new DateOnly(2025, 6, 15), new DateOnly(2026, 6, 15),
            NcbPercent: 30, ClaimFree: true, Gen);

        var pdf = new NcbCertificateDocument(data).GeneratePdf();

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
    }
}

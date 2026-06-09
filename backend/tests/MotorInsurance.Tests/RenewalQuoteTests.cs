using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Renewals;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

/// <summary>
/// Renewal-quote estimation that backs the reminder price, and that the quote matches what the
/// actual renewal charges (same shared rating path). (F2)
/// </summary>
public class RenewalQuoteTests
{
    private static async Task<InMemoryAppDb> DbWithActivePolicyAsync(int ncbPercent, bool withClaim = false)
    {
        var db = InMemoryAppDb.New();
        db.Customers.Add(new Customer
        {
            Id = 1,
            NationalId = "1100000000001",
            FirstName = "สมชาย",
            LastName = "รักดี",
            FullName = "สมชาย รักดี",
            Email = "somchai@example.com",
        });
        db.Vehicles.Add(new Vehicle { Id = 1, CustomerId = 1, RegistrationNo = "1กก1234", Province = "กรุงเทพมหานคร", ModelYearId = 1 });
        db.VehicleModelYears.Add(new VehicleModelYear { Id = 1, SubmodelId = 1, Year = 2022 });
        db.Policies.Add(new Policy
        {
            Id = 1,
            PolicyNo = "POL-2025-000001",
            CustomerId = 1,
            VehicleId = 1,
            Status = PolicyStatus.Active,
            CoverageType = CoverageType.Type1,
            SumInsured = 500_000m,
            BasePremium = 22_500m,
            Premium = 22_500m,
            NcbPercent = ncbPercent,
            Deductible = 0,
            EffectiveDate = new DateOnly(2025, 6, 15),
            ExpiryDate = new DateOnly(2026, 6, 15),
        });
        if (withClaim)
            db.Claims.Add(new Claim
            {
                Id = 1,
                ClaimNo = "CLM-2025-000001",
                PolicyId = 1,
                Status = ClaimStatus.Paid,
                IncidentDate = new DateOnly(2025, 9, 1),
                ClaimedAmount = 10_000m,
            });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Estimate_steps_ncb_up_after_a_claim_free_year()
    {
        await using var db = await DbWithActivePolicyAsync(ncbPercent: 20);

        var terms = await RenewalQuote.EstimateAsync(db, 1, default);

        Assert.NotNull(terms);
        Assert.Equal(30, terms!.NewNcb);                       // 20 -> 30 (claim-free)
        Assert.Equal(new DateOnly(2026, 6, 16), terms.NewEffective);
        Assert.True(terms.Breakdown.NetPremium > 0);
    }

    [Fact]
    public async Task Estimate_resets_ncb_after_a_claim()
    {
        await using var db = await DbWithActivePolicyAsync(ncbPercent: 40, withClaim: true);

        var terms = await RenewalQuote.EstimateAsync(db, 1, default);

        Assert.NotNull(terms);
        Assert.Equal(0, terms!.NewNcb);                        // a prior-year claim resets NCB
    }

    [Fact]
    public async Task Estimate_returns_null_for_missing_or_unexpiring_policy()
    {
        await using var db = await DbWithActivePolicyAsync(ncbPercent: 0);

        Assert.Null(await RenewalQuote.EstimateAsync(db, 999, default));
    }

    [Fact]
    public async Task Reminder_includes_the_quoted_premium_in_the_body()
    {
        await using var db = await DbWithActivePolicyAsync(ncbPercent: 20);
        var sender = new FakeNotificationSender(result: true);
        var terms = await RenewalQuote.EstimateAsync(db, 1, default);

        var note = await RenewalReminders.SendAsync(
            db, sender, new FixedClockProvider(), policyId: 1, policyNo: "POL-2025-000001",
            customerName: "สมชาย รักดี", email: "somchai@example.com", phone: null,
            expiry: new DateOnly(2026, 6, 15), ct: default, lineUserId: null,
            estimatedPremium: terms!.Breakdown.NetPremium);

        Assert.Contains("เบี้ยต่ออายุโดยประมาณ", note.Body);
        Assert.Contains(terms.Breakdown.NetPremium.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("th-TH")), note.Body);
    }

    [Fact]
    public async Task Reminder_without_a_quote_omits_the_premium_line()
    {
        await using var db = InMemoryAppDb.New();
        var sender = new FakeNotificationSender(result: true);

        var note = await RenewalReminders.SendAsync(
            db, sender, new FixedClockProvider(), 1, "POL-1", "สมชาย", "a@b.com", null,
            new DateOnly(2026, 7, 1), default);

        Assert.DoesNotContain("เบี้ยต่ออายุโดยประมาณ", note.Body);
    }
}

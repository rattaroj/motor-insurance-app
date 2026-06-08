using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Endpoints.Policies;
using MotorInsurance.Api.Endpoints.Renewals;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Policies;
using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

// NOTE: EF InMemory does NOT support temporal tables or rowversion concurrency.
// These cover handler LOGIC only. Temporal/rowversion behaviour must be tested
// against real SQL Server (Testcontainers) where Liquibase has migrated the schema.
public class PolicyIssuanceSliceTests
{
    private sealed class TestDb : DbContext, IAppDbContext
    {
        public TestDb(DbContextOptions options) : base(options) { }
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<CustomerTitle> CustomerTitles => Set<CustomerTitle>();
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
        public DbSet<VehicleBrand> VehicleBrands => Set<VehicleBrand>();
        public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
        public DbSet<VehicleSubmodel> VehicleSubmodels => Set<VehicleSubmodel>();
        public DbSet<VehicleModelYear> VehicleModelYears => Set<VehicleModelYear>();
        public DbSet<Province> Provinces => Set<Province>();
        public DbSet<District> Districts => Set<District>();
        public DbSet<PostalCode> PostalCodes => Set<PostalCode>();
        public DbSet<Subdistrict> Subdistricts => Set<Subdistrict>();
        public DbSet<Quotation> Quotations => Set<Quotation>();
        public DbSet<QuotationDriver> QuotationDrivers => Set<QuotationDriver>();
        public DbSet<Rider> Riders => Set<Rider>();
        public DbSet<PremiumRate> PremiumRates => Set<PremiumRate>();
        public DbSet<AgeLoadingBand> AgeLoadingBands => Set<AgeLoadingBand>();
        public DbSet<RatingSetting> RatingSettings => Set<RatingSetting>();
        public DbSet<QuotationRider> QuotationRiders => Set<QuotationRider>();
        public DbSet<PolicyRider> PolicyRiders => Set<PolicyRider>();
        public DbSet<Policy> Policies => Set<Policy>();
        public DbSet<Endorsement> Endorsements => Set<Endorsement>();
        public DbSet<Claim> Claims => Set<Claim>();
        public DbSet<ClaimPhoto> ClaimPhotos => Set<ClaimPhoto>();
        public DbSet<Garage> Garages => Set<Garage>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        protected override void OnModelCreating(ModelBuilder mb)
        {
            // Ignore temporal/rowversion bits that InMemory can't model.
            mb.Entity<Policy>().Ignore(p => p.RowVersion);
            mb.Entity<Claim>().Ignore(c => c.RowVersion);
            mb.Entity<Payment>().Ignore(p => p.RowVersion);

            // Keyless/composite-key auth entities need explicit keys (no IEntityTypeConfiguration here).
            mb.Entity<Permission>().HasKey(p => p.Code);
            mb.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
            mb.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionCode });
            mb.Entity<QuotationRider>().HasKey(x => new { x.QuotationId, x.RiderId });
            mb.Entity<PolicyRider>().HasKey(x => new { x.PolicyId, x.RiderId });
            mb.Entity<RatingSetting>().HasKey(x => x.Code);
        }
    }

    private sealed class FakeDocNo : IDocumentNumberGenerator
    {
        private int _n;
        public Task<string> NextAsync(string prefix, CancellationToken ct = default)
            => Task.FromResult($"{prefix}-TEST-{++_n:D4}");
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc);
    }

    private static TestDb NewDb() =>
        new(new DbContextOptionsBuilder<TestDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Quotation_then_issue_creates_policy_and_pending_premium()
    {
        await using var db = NewDb();
        var clock = new FixedClock();
        var docNo = new FakeDocNo();

        db.Customers.Add(new Customer { Id = 1, NationalId = "1100000000001", FirstName = "สมชาย", LastName = "ทดสอบ", FullName = "สมชาย ทดสอบ" });
        db.Vehicles.Add(new Vehicle { Id = 1, CustomerId = 1, RegistrationNo = "กก1234", Province = "กทม", ModelYearId = 1 });
        await db.SaveChangesAsync();

        db.Quotations.Add(new Quotation
        {
            Id = 1,
            QuotationNo = "QUO-TEST-0001",
            CustomerId = 1,
            VehicleId = 1,
            CoverageType = CoverageType.Type1,
            SumInsured = 500_000m,
            Premium = PremiumCalculator.Calculate(CoverageType.Type1, 500_000m),
            ValidUntil = new DateOnly(2026, 6, 27),
        });
        await db.SaveChangesAsync();
        const long quoteId = 1;

        var policyId = await new IssuePolicyEndpoint(db, docNo, clock)
            .IssueAsync(new IssuePolicyRequest(quoteId, new DateOnly(2026, 6, 1)), default);

        var policy = await db.Policies.FindAsync(policyId);
        Assert.NotNull(policy);
        Assert.Equal(PolicyStatus.Issued, policy!.Status);
        Assert.Equal(new DateOnly(2027, 6, 1), policy.ExpiryDate);

        var premium = await db.Payments.SingleAsync(p => p.PolicyId == policyId);
        Assert.Equal(PaymentDirection.Inbound, premium.Direction);
        Assert.Equal(PaymentStatus.Pending, premium.Status);
        Assert.Equal(policy.Premium, premium.Amount);
    }

    [Fact]
    public async Task Issue_with_installments_creates_a_plan_and_scheduled_payments()
    {
        await using var db = NewDb();
        var clock = new FixedClock();

        db.Customers.Add(new Customer { Id = 1, NationalId = "1100000000001", FirstName = "ก", LastName = "ข", FullName = "ก ข" });
        db.Vehicles.Add(new Vehicle { Id = 1, CustomerId = 1, RegistrationNo = "กก1234", Province = "กทม", ModelYearId = 1 });
        db.Quotations.Add(new Quotation
        {
            Id = 1, QuotationNo = "QUO-TEST-0001", CustomerId = 1, VehicleId = 1,
            CoverageType = CoverageType.Type1, SumInsured = 500_000m,
            Premium = PremiumCalculator.Calculate(CoverageType.Type1, 500_000m),
            ValidUntil = new DateOnly(2026, 6, 27),
        });
        await db.SaveChangesAsync();

        var policyId = await new IssuePolicyEndpoint(db, new FakeDocNo(), clock)
            .IssueAsync(new IssuePolicyRequest(1, new DateOnly(2026, 6, 1), Installments: 3), default);

        var policy = await db.Policies.FindAsync(policyId);
        var plan = await db.InstallmentPlans.SingleAsync(p => p.PolicyId == policyId);
        Assert.Equal(3, plan.Installments);
        Assert.Equal(InstallmentPlanStatus.Active, plan.Status);

        var payments = await db.Payments.Where(p => p.PolicyId == policyId)
            .OrderBy(p => p.InstallmentSeq).ToListAsync();
        Assert.Equal(3, payments.Count);
        Assert.All(payments, p => Assert.Equal(PaymentStatus.Pending, p.Status));
        // Installments sum to premium + the flat financing fee.
        Assert.Equal(policy!.Premium + InstallmentPlanning.FlatFee, payments.Sum(p => p.Amount));
        // First due today (down payment); the next FrequencyDays later.
        var today = DateOnly.FromDateTime(clock.UtcNow.Date);
        Assert.Equal(today, payments[0].DueDate);
        Assert.Equal(today.AddDays(InstallmentPlanning.FrequencyDays), payments[1].DueDate);
    }

    // Active policy expiring 2026-06-15 — within the 60-day renewal window for the fixed clock (2026-05-28).
    private static async Task<TestDb> DbWithActivePolicyAsync(int ncbPercent)
    {
        var db = NewDb();
        db.Customers.Add(new Customer { Id = 1, NationalId = "1100000000001", FirstName = "ก", LastName = "ข", FullName = "ก ข" });
        db.Policies.Add(new Policy
        {
            Id = 1, PolicyNo = "POL-TEST-0001", CustomerId = 1, VehicleId = 1,
            Status = PolicyStatus.Active, CoverageType = CoverageType.Type1,
            SumInsured = 500_000m, BasePremium = 22_500m, Premium = 22_500m,
            NcbPercent = ncbPercent, Deductible = 0,
            EffectiveDate = new DateOnly(2025, 6, 15), ExpiryDate = new DateOnly(2026, 6, 15),
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Renewal_steps_ncb_up_after_a_claim_free_year()
    {
        await using var db = await DbWithActivePolicyAsync(ncbPercent: 20);
        var renewalId = await new RenewPolicyEndpoint(db, new FakeDocNo(), new FixedClock())
            .RenewAsync(1, new RenewPolicyRequest(null), default);

        var renewal = await db.Policies.FindAsync(renewalId);
        Assert.Equal(30, renewal!.NcbPercent);   // 20 -> 30 (claim-free)
    }

    [Fact]
    public async Task Renewal_resets_ncb_after_a_claim()
    {
        await using var db = await DbWithActivePolicyAsync(ncbPercent: 40);
        db.Claims.Add(new Claim
        {
            Id = 1, ClaimNo = "CLM-TEST-0001", PolicyId = 1,
            Status = ClaimStatus.Approved, IncidentDate = new DateOnly(2025, 12, 1), ClaimedAmount = 10_000m,
        });
        await db.SaveChangesAsync();

        var renewalId = await new RenewPolicyEndpoint(db, new FakeDocNo(), new FixedClock())
            .RenewAsync(1, new RenewPolicyRequest(null), default);

        var renewal = await db.Policies.FindAsync(renewalId);
        Assert.Equal(0, renewal!.NcbPercent);    // claim in prior year resets NCB
    }
}

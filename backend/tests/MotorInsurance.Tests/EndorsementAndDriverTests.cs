using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Endpoints.Policies;
using MotorInsurance.Api.Endpoints.Quotations;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Xunit;

namespace MotorInsurance.Tests;

// EF InMemory: handler-logic coverage only (no temporal/rowversion). See PolicyIssuanceSliceTests.
public class EndorsementAndDriverTests
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
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<AppUser> Users => Set<AppUser>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<Policy>().Ignore(p => p.RowVersion);
            mb.Entity<Claim>().Ignore(c => c.RowVersion);
            mb.Entity<Payment>().Ignore(p => p.RowVersion);
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

    private static async Task<TestDb> DbWithPolicyAsync(PolicyStatus status)
    {
        var db = NewDb();
        db.Customers.Add(new Customer { Id = 1, NationalId = "1100000000001", FirstName = "เดิม", LastName = "ชื่อ", FullName = "เดิม ชื่อ", Phone = "0810000000" });
        db.Policies.Add(new Policy
        {
            Id = 1, PolicyNo = "POL-TEST-0001", CustomerId = 1, VehicleId = 1,
            Status = status, CoverageType = CoverageType.Type1, SumInsured = 500_000m, Premium = 10_000m,
        });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Endorse_applies_change_and_records_old_new()
    {
        await using var db = await DbWithPolicyAsync(PolicyStatus.Active);
        var sut = new CreateEndorsementEndpoint(db, new FakeDocNo(), new FixedClock());

        var nos = await sut.EndorseAsync(1,
            new CreateEndorsementRequest("ใหม่ ชื่อ", "0899999999", null, new DateOnly(2026, 6, 1), "ย้ายที่อยู่"),
            default);

        Assert.Equal(2, nos.Count); // FullName + Phone changed
        var customer = await db.Customers.FindAsync(1L);
        Assert.Equal("ใหม่ ชื่อ", customer!.FullName);
        Assert.Equal("0899999999", customer.Phone);

        var fullNameEndo = await db.Endorsements.SingleAsync(e => e.FieldName == "FullName");
        Assert.Equal("เดิม ชื่อ", fullNameEndo.OldValue);
        Assert.Equal("ใหม่ ชื่อ", fullNameEndo.NewValue);
    }

    [Fact]
    public async Task Endorse_rejects_when_policy_not_endorsable()
    {
        await using var db = await DbWithPolicyAsync(PolicyStatus.Cancelled);
        var sut = new CreateEndorsementEndpoint(db, new FakeDocNo(), new FixedClock());

        await Assert.ThrowsAsync<ConflictException>(() => sut.EndorseAsync(1,
            new CreateEndorsementRequest("ใหม่ ชื่อ", null, null, new DateOnly(2026, 6, 1), null), default));
    }

    [Fact]
    public async Task Endorse_rejects_when_no_change()
    {
        await using var db = await DbWithPolicyAsync(PolicyStatus.Active);
        var sut = new CreateEndorsementEndpoint(db, new FakeDocNo(), new FixedClock());

        await Assert.ThrowsAsync<ConflictException>(() => sut.EndorseAsync(1,
            new CreateEndorsementRequest("เดิม ชื่อ", "0810000000", null, new DateOnly(2026, 6, 1), null), default));
    }

    [Theory]
    [InlineData(0, false)]   // no drivers
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]   // over the cap
    public void Quotation_driver_count_validation(int count, bool expectedValid)
    {
        var drivers = Enumerable.Range(0, count)
            .Select(_ => new DriverInput("คน ขับ", "1100000000001", "uploads/idcards/x.jpg"))
            .ToList();
        var req = new CreateQuotationRequest(1, 1, CoverageType.Type1, 500_000m, drivers);

        var result = new CreateQuotationValidator().Validate(req);
        Assert.Equal(expectedValid, result.IsValid);
    }
}

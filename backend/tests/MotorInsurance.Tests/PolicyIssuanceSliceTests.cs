using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Endpoints.Policies;
using MotorInsurance.Application.Common.Interfaces;
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
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
        public DbSet<VehicleBrand> VehicleBrands => Set<VehicleBrand>();
        public DbSet<VehicleModel> VehicleModels => Set<VehicleModel>();
        public DbSet<VehicleSubmodel> VehicleSubmodels => Set<VehicleSubmodel>();
        public DbSet<VehicleModelYear> VehicleModelYears => Set<VehicleModelYear>();
        public DbSet<Quotation> Quotations => Set<Quotation>();
        public DbSet<Policy> Policies => Set<Policy>();
        public DbSet<Claim> Claims => Set<Claim>();
        public DbSet<Payment> Payments => Set<Payment>();
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

        db.Customers.Add(new Customer { Id = 1, NationalId = "1100000000001", FullName = "สมชาย ทดสอบ" });
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
}

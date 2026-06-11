using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Tests;

/// <summary>
/// Shared in-memory <see cref="IAppDbContext"/> for handler-logic tests (EF InMemory: no temporal
/// tables or rowversion concurrency — those need real SQL Server). Mirrors AppDbContext's DbSets.
/// </summary>
public sealed class InMemoryAppDb : DbContext, IAppDbContext
{
    public InMemoryAppDb(DbContextOptions options) : base(options) { }

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
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
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

        // Keyless/composite-key entities need explicit keys (no IEntityTypeConfiguration here).
        mb.Entity<Permission>().HasKey(p => p.Code);
        mb.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        mb.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionCode });
        mb.Entity<QuotationRider>().HasKey(x => new { x.QuotationId, x.RiderId });
        mb.Entity<PolicyRider>().HasKey(x => new { x.PolicyId, x.RiderId });
        mb.Entity<RatingSetting>().HasKey(x => x.Code);
    }

    public static InMemoryAppDb New() =>
        new(new DbContextOptionsBuilder<InMemoryAppDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}

/// <summary>Deterministic document-number generator: PREFIX-TEST-0001, 0002, …</summary>
public sealed class FakeDocNoGenerator : IDocumentNumberGenerator
{
    private int _n;
    public Task<string> NextAsync(string prefix, CancellationToken ct = default)
        => Task.FromResult($"{prefix}-TEST-{++_n:D4}");
}

/// <summary>Fixed clock for reproducible dates.</summary>
public sealed class FixedClockProvider : IDateTimeProvider
{
    private readonly DateTime _now;
    public FixedClockProvider(DateTime? now = null)
        => _now = now ?? new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc);
    public DateTime UtcNow => _now;
}

/// <summary>Records every message and returns a preset success/failure result.</summary>
public sealed class FakeNotificationSender : INotificationSender
{
    private readonly bool _result;
    public FakeNotificationSender(bool result = true) => _result = result;

    public List<NotificationMessage> Sent { get; } = new();

    public Task<bool> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        Sent.Add(message);
        return Task.FromResult(_result);
    }
}

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Common;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUser currentUser,
        IDateTimeProvider clock) : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

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
    public DbSet<QuotationRider> QuotationRiders => Set<QuotationRider>();
    public DbSet<PolicyRider> PolicyRiders => Set<PolicyRider>();
    public DbSet<Policy> Policies => Set<Policy>();
    public DbSet<Endorsement> Endorsements => Set<Endorsement>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Map the shared BaseEntity audit columns (snake_case) on every entity that has them,
        // so we don't repeat these four lines in each IEntityTypeConfiguration. CreatedAt is
        // already mapped per-config; the rest are uniform across all audited tables.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType)) continue;

            var b = modelBuilder.Entity(entityType.ClrType);
            b.Property(nameof(BaseEntity.CreatedUser)).HasColumnName("created_user").HasMaxLength(100);
            b.Property(nameof(BaseEntity.UpdatedUser)).HasColumnName("updated_user").HasMaxLength(100);
            b.Property(nameof(BaseEntity.UpdatedAt)).HasColumnName("updated_at");
            b.Property(nameof(BaseEntity.IsActive)).HasColumnName("is_active");
        }

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    /// <summary>
    /// Stamps audit columns on every tracked <see cref="BaseEntity"/>: CreatedUser/CreatedAt
    /// on insert, UpdatedUser/UpdatedAt on update. User is the current username (null for
    /// non-request work such as data seeding). IsActive is left to the entity/DB default.
    /// </summary>
    private void ApplyAuditInfo()
    {
        var now = _clock.UtcNow;
        var user = _currentUser.Username;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedUser = user;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedUser = user;
                    break;
            }
        }
    }
}

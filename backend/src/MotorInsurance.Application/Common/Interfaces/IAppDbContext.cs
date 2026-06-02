using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the EF DbContext so the Application layer
/// depends on an interface, not Infrastructure (Clean Architecture).
/// </summary>
public interface IAppDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<VehicleBrand> VehicleBrands { get; }
    DbSet<VehicleModel> VehicleModels { get; }
    DbSet<VehicleSubmodel> VehicleSubmodels { get; }
    DbSet<VehicleModelYear> VehicleModelYears { get; }
    DbSet<Province> Provinces { get; }
    DbSet<District> Districts { get; }
    DbSet<PostalCode> PostalCodes { get; }
    DbSet<Subdistrict> Subdistricts { get; }
    DbSet<Quotation> Quotations { get; }
    DbSet<Policy> Policies { get; }
    DbSet<Claim> Claims { get; }
    DbSet<Payment> Payments { get; }
    DbSet<AppUser> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrentUser
{
    long? UserId { get; }
    string? Username { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Permissions { get; }
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

/// <summary>Generates business document numbers (POL-2026-000001 etc.).</summary>
public interface IDocumentNumberGenerator
{
    Task<string> NextAsync(string prefix, CancellationToken ct = default);
}

/// <summary>
/// Reads system-versioned (temporal) policy history. Lives behind an interface
/// because TemporalAll() is a SQL Server provider API and must stay in Infrastructure.
/// </summary>
public interface IPolicyHistoryReader
{
    Task<IReadOnlyList<PolicyHistoryDto>> GetHistoryAsync(long policyId, CancellationToken ct = default);
}

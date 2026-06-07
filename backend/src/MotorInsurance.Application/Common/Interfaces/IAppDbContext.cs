using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the EF DbContext so the Application layer
/// depends on an interface, not Infrastructure (Clean Architecture).
/// </summary>
public interface IAppDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<CustomerTitle> CustomerTitles { get; }
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
    DbSet<QuotationDriver> QuotationDrivers { get; }
    DbSet<Rider> Riders { get; }
    DbSet<PremiumRate> PremiumRates { get; }
    DbSet<AgeLoadingBand> AgeLoadingBands { get; }
    DbSet<RatingSetting> RatingSettings { get; }
    DbSet<QuotationRider> QuotationRiders { get; }
    DbSet<PolicyRider> PolicyRiders { get; }
    DbSet<Policy> Policies { get; }
    DbSet<Endorsement> Endorsements { get; }
    DbSet<Claim> Claims { get; }
    DbSet<ClaimPhoto> ClaimPhotos { get; }
    DbSet<Garage> Garages { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Notification> Notifications { get; }
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

/// <summary>
/// A message to deliver through some channel (Email/Sms/Line). Channel is informational.
/// An optional PDF attachment is honoured by the SMTP sender (ignored by others).
/// </summary>
public record NotificationMessage(
    string Channel, string Recipient, string Subject, string Body,
    byte[]? AttachmentBytes = null, string? AttachmentName = null);

/// <summary>
/// Delivers a notification. The default dev implementation logs it (delivery is recorded in the
/// <c>notification</c> table by the caller); swap in real SMTP/LINE without touching call sites.
/// </summary>
public interface INotificationSender
{
    Task<bool> SendAsync(NotificationMessage message, CancellationToken ct = default);
}

/// <summary>Generates business document numbers (POL-2026-000001 etc.).</summary>
public interface IDocumentNumberGenerator
{
    Task<string> NextAsync(string prefix, CancellationToken ct = default);
}

/// <summary>
/// Stores uploaded files (e.g. driver ID-card images) and returns the relative path
/// under which they are served. Implementation lives in Infrastructure.
/// </summary>
public interface IFileStorage
{
    /// <summary>True when the given content type is an allowed image type.</summary>
    bool IsAllowed(string? contentType);

    /// <summary>Persists the stream and returns the relative path (e.g. "uploads/idcards/{guid}.jpg").</summary>
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default);
}

/// <summary>
/// Reads system-versioned (temporal) policy history. Lives behind an interface
/// because TemporalAll() is a SQL Server provider API and must stay in Infrastructure.
/// </summary>
public interface IPolicyHistoryReader
{
    Task<IReadOnlyList<PolicyHistoryDto>> GetHistoryAsync(long policyId, CancellationToken ct = default);
}

/// <summary>An open claim with the time it entered its current status (from the temporal history).</summary>
public record ClaimAgingRow(
    long Id, string ClaimNo, string PolicyNo, ClaimStatus Status, decimal ClaimedAmount, DateTime StatusSince);

/// <summary>
/// Reads claim aging from the system-versioned (temporal) claim history: for each open claim,
/// the moment it entered its current status. Behind an interface because TemporalAll() is a
/// SQL Server provider API that must stay in Infrastructure.
/// </summary>
public interface IClaimAgingReader
{
    Task<IReadOnlyList<ClaimAgingRow>> GetOpenAsync(CancellationToken ct = default);
}

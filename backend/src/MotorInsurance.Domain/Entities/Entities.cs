using MotorInsurance.Domain.Common;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Domain.Entities;

public class Customer : BaseEntity
{
    public string NationalId { get; set; } = default!;
    public string? Title { get; set; }                  // คำนำหน้า (นาย/นาง/นางสาว) — optional
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    /// <summary>Derived display name, kept in sync from Title/FirstName/LastName via <see cref="SyncFullName"/>.</summary>
    public string FullName { get; set; } = default!;
    public DateOnly? BirthDate { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    // Address: free-text line + optional references to the geography master (all nullable).
    public string? AddressLine { get; set; }
    public long? ProvinceId { get; set; }
    public long? DistrictId { get; set; }
    public long? SubdistrictId { get; set; }
    public long? PostalCodeId { get; set; }
    public Province? Province { get; set; }
    public District? District { get; set; }
    public Subdistrict? Subdistrict { get; set; }
    public PostalCode? PostalCode { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

    /// <summary>Compose the display name from its parts: "[title ]first last".</summary>
    public static string ComposeFullName(string? title, string firstName, string lastName)
    {
        var name = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(title) ? name : $"{title} {name}".Trim();
    }

    /// <summary>Split a full name into (first, rest) on the first space — for backfilling parts.</summary>
    public static (string FirstName, string LastName) SplitName(string fullName)
    {
        var t = (fullName ?? string.Empty).Trim();
        var i = t.IndexOf(' ');
        return i < 0 ? (t, string.Empty) : (t[..i], t[(i + 1)..].Trim());
    }

    /// <summary>Refresh <see cref="FullName"/> from the current Title/FirstName/LastName.</summary>
    public void SyncFullName() => FullName = ComposeFullName(Title, FirstName, LastName);
}

public class Vehicle : BaseEntity
{
    public long CustomerId { get; set; }
    public string RegistrationNo { get; set; } = default!;
    public string Province { get; set; } = default!;
    public long ModelYearId { get; set; }
    public string? ChassisNo { get; set; }
    public Customer Customer { get; set; } = default!;
    public VehicleModelYear ModelYear { get; set; } = default!;
}

// ----- Vehicle master data (cascading lookups; no audit columns) -----
public class VehicleBrand
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public ICollection<VehicleModel> Models { get; set; } = new List<VehicleModel>();
}

public class VehicleModel
{
    public long Id { get; set; }
    public long BrandId { get; set; }
    public string Name { get; set; } = default!;
    public VehicleBrand Brand { get; set; } = default!;
    public ICollection<VehicleSubmodel> Submodels { get; set; } = new List<VehicleSubmodel>();
}

public class VehicleSubmodel
{
    public long Id { get; set; }
    public long ModelId { get; set; }
    public string Name { get; set; } = default!;
    public Powertrain Powertrain { get; set; }
    public VehicleModel Model { get; set; } = default!;
    public ICollection<VehicleModelYear> ModelYears { get; set; } = new List<VehicleModelYear>();
}

public class VehicleModelYear
{
    public long Id { get; set; }
    public long SubmodelId { get; set; }
    public int Year { get; set; }
    public VehicleSubmodel Submodel { get; set; } = default!;
}

// ----- Thai administrative-division master data (cascading lookups; no audit columns).
// Ids are the official codes (province/district/subdistrict); postal_code.Id is the
// 5-digit code as a number. A subdistrict references exactly one postal code. -----
public class Province
{
    public long Id { get; set; }
    public string NameTh { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public ICollection<District> Districts { get; set; } = new List<District>();
}

public class District
{
    public long Id { get; set; }
    public long ProvinceId { get; set; }
    public string NameTh { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public Province Province { get; set; } = default!;
    public ICollection<Subdistrict> Subdistricts { get; set; } = new List<Subdistrict>();
}

public class PostalCode
{
    public long Id { get; set; }
    public string Code { get; set; } = default!;
    public ICollection<Subdistrict> Subdistricts { get; set; } = new List<Subdistrict>();
}

public class Subdistrict
{
    public long Id { get; set; }
    public long DistrictId { get; set; }
    public long PostalCodeId { get; set; }
    public string NameTh { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public District District { get; set; } = default!;
    public PostalCode PostalCode { get; set; } = default!;
}

public class Quotation : BaseEntity
{
    public string QuotationNo { get; set; } = default!;
    public long CustomerId { get; set; }
    public long VehicleId { get; set; }
    public CoverageType CoverageType { get; set; }
    public decimal SumInsured { get; set; }
    public decimal Premium { get; set; }
    public DateOnly ValidUntil { get; set; }
    public Customer Customer { get; set; } = default!;
    public Vehicle Vehicle { get; set; } = default!;
}

public class Policy : AuditableEntity
{
    public string PolicyNo { get; set; } = default!;
    public long? QuotationId { get; set; }
    public long CustomerId { get; set; }
    public long VehicleId { get; set; }
    public PolicyStatus Status { get; set; }
    public CoverageType CoverageType { get; set; }
    public decimal SumInsured { get; set; }
    public decimal Premium { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public long? PreviousPolicyId { get; set; }   // set on renewal

    public Customer Customer { get; set; } = default!;
    public Vehicle Vehicle { get; set; } = default!;
    public Quotation? Quotation { get; set; }
    public Policy? PreviousPolicy { get; set; }
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class Claim : AuditableEntity
{
    public string ClaimNo { get; set; } = default!;
    public long PolicyId { get; set; }
    public ClaimStatus Status { get; set; }
    public DateOnly IncidentDate { get; set; }
    public string? Description { get; set; }
    public decimal ClaimedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public string? RejectReason { get; set; }
    public Policy Policy { get; set; } = default!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class Payment : AuditableEntity
{
    public string PaymentNo { get; set; } = default!;
    public PaymentDirection Direction { get; set; }
    public PaymentStatus Status { get; set; }
    public long? PolicyId { get; set; }
    public long? ClaimId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? ReferenceNo { get; set; }
    public Policy? Policy { get; set; }
    public Claim? Claim { get; set; }
}

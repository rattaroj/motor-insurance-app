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

/// <summary>Customer title (คำนำหน้าชื่อ) master — governs the title select options.</summary>
public class CustomerTitle
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
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
    /// <summary>Net premium (after NCB/deductible, plus riders). See <see cref="BasePremium"/>.</summary>
    public decimal Premium { get; set; }
    /// <summary>Pre-discount base premium (sum insured × coverage rate) — the start of the breakdown.</summary>
    public decimal BasePremium { get; set; }
    public int NcbPercent { get; set; }          // ส่วนลดประวัติดี: 0/20/30/40/50
    public decimal Deductible { get; set; }       // ค่าเสียหายส่วนแรก
    public DateOnly ValidUntil { get; set; }
    public Customer Customer { get; set; } = default!;
    public Vehicle Vehicle { get; set; } = default!;
    // Named drivers (new-law requirement): 1–5 per quotation, each with an ID-card image.
    public ICollection<QuotationDriver> Drivers { get; set; } = new List<QuotationDriver>();
    public ICollection<QuotationRider> Riders { get; set; } = new List<QuotationRider>();
}

/// <summary>A named driver declared on a quotation (max 5), with an attached ID-card image.</summary>
public class QuotationDriver : BaseEntity
{
    public long QuotationId { get; set; }
    public string FullName { get; set; } = default!;
    public string NationalId { get; set; } = default!;
    public string IdCardImagePath { get; set; } = default!;   // relative path, e.g. "uploads/idcards/xxxx.jpg"
    public Quotation Quotation { get; set; } = default!;
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
    /// <summary>Net premium (after NCB/deductible, plus riders). See <see cref="BasePremium"/>.</summary>
    public decimal Premium { get; set; }
    /// <summary>Pre-discount base premium — carried from the quotation/recomputed on renewal.</summary>
    public decimal BasePremium { get; set; }
    public int NcbPercent { get; set; }          // ส่วนลดประวัติดี: 0/20/30/40/50
    public decimal Deductible { get; set; }       // ค่าเสียหายส่วนแรก
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public long? PreviousPolicyId { get; set; }   // set on renewal

    public Customer Customer { get; set; } = default!;
    public Vehicle Vehicle { get; set; } = default!;
    public Quotation? Quotation { get; set; }
    public Policy? PreviousPolicy { get; set; }
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Endorsement> Endorsements { get; set; } = new List<Endorsement>();
    public ICollection<PolicyRider> Riders { get; set; } = new List<PolicyRider>();
}

/// <summary>Add-on rider (ความคุ้มครองเสริม) master — a selectable coverage with a flat premium.</summary>
public class Rider
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal Premium { get; set; }
}

/// <summary>Join: a rider selected on a quotation.</summary>
public class QuotationRider
{
    public long QuotationId { get; set; }
    public long RiderId { get; set; }
    public Quotation Quotation { get; set; } = default!;
    public Rider Rider { get; set; } = default!;
}

/// <summary>Join: a rider carried on a policy.</summary>
public class PolicyRider
{
    public long PolicyId { get; set; }
    public long RiderId { get; set; }
    public Policy Policy { get; set; } = default!;
    public Rider Rider { get; set; } = default!;
}

/// <summary>
/// A policy endorsement (สลักหลัง): the formal record of a change to an issued/active
/// policy. Editing the customer data of a customer who already holds a policy MUST go
/// through one of these (one row per changed field), capturing old → new values.
/// </summary>
public class Endorsement : BaseEntity
{
    public string EndorsementNo { get; set; } = default!;      // END-YYYY-######
    public long PolicyId { get; set; }
    public string FieldName { get; set; } = default!;          // e.g. "FullName", "Phone", "Email"
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Note { get; set; }
    public Policy Policy { get; set; } = default!;
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
    public long? GarageId { get; set; }                  // อู่/ศูนย์ซ่อมที่มอบหมาย
    public string? SurveyorName { get; set; }            // ผู้สำรวจภัย
    public Policy Policy { get; set; } = default!;
    public Garage? Garage { get; set; }
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<ClaimPhoto> Photos { get; set; } = new List<ClaimPhoto>();
}

/// <summary>Repair-shop (อู่/ศูนย์ซ่อม) master — assignable to a claim.</summary>
public class Garage
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
}

/// <summary>A damage photo attached to a claim (relative path, served via static files).</summary>
public class ClaimPhoto
{
    public long Id { get; set; }
    public long ClaimId { get; set; }
    public string ImagePath { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public Claim Claim { get; set; } = default!;
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

/// <summary>A persisted, auditable record of a notification sent (e.g. a renewal reminder).</summary>
public class Notification
{
    public long Id { get; set; }
    public long? PolicyId { get; set; }
    public string Channel { get; set; } = default!;     // Email / Sms / Line / Log
    public string Recipient { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string Status { get; set; } = default!;       // Sent / Failed
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Policy? Policy { get; set; }
}

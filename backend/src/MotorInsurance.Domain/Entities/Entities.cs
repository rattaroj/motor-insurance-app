using MotorInsurance.Domain.Common;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Domain.Entities;

public class Customer : BaseEntity
{
    public string NationalId { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
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

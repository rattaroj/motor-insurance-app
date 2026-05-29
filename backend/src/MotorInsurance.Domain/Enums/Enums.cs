namespace MotorInsurance.Domain.Enums;

public enum PolicyStatus { Draft, Quoted, Issued, Active, Cancelled, Expired }

public enum ClaimStatus { Filed, UnderReview, Assessment, Approved, Rejected, Paid, Closed }

public enum PaymentStatus { Pending, Paid, Failed, Refunded }

public enum PaymentDirection { Inbound, Outbound }

public enum CoverageType { Type1, Type2Plus, Type3Plus, Type3 }

public enum Powertrain { Gasoline, Diesel, Electric, Hybrid }

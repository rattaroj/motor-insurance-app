namespace MotorInsurance.Application.Common.Authorization;

/// <summary>
/// Permission code constants. MUST match the seed values in db/scripts/V005__auth.sql
/// (the permission.code column) and the frontend mirror in lib/auth/permissions.ts.
/// </summary>
public static class Permissions
{
    public const string CustomerRead = "customer.read";
    public const string CustomerWrite = "customer.write";

    public const string VehicleRead = "vehicle.read";
    public const string VehicleWrite = "vehicle.write";

    public const string LookupRead = "lookup.read";
    public const string LookupManage = "lookup.manage";

    public const string QuotationRead = "quotation.read";
    public const string QuotationWrite = "quotation.write";

    public const string PolicyRead = "policy.read";
    public const string PolicyIssue = "policy.issue";
    public const string PolicyActivate = "policy.activate";
    public const string PolicyCancel = "policy.cancel";
    public const string PolicyRenew = "policy.renew";
    public const string PolicyEndorse = "policy.endorse";

    public const string ClaimRead = "claim.read";
    public const string ClaimFile = "claim.file";
    public const string ClaimReview = "claim.review";
    public const string ClaimApprove = "claim.approve";
    public const string ClaimReject = "claim.reject";

    public const string PaymentRead = "payment.read";
    public const string PaymentSettle = "payment.settle";

    public const string DashboardRead = "dashboard.read";
}

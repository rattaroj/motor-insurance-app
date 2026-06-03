using MotorInsurance.Application.Common.Models;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Api.Endpoints.Policies;

/// <summary>Shared Policy -> PolicyDto projection used by the policy read endpoints.</summary>
internal static class PolicyMapping
{
    public static PolicyDto ToDto(Policy p) => new(
        p.Id, p.PolicyNo, p.CustomerId, p.Customer.FullName,
        p.VehicleId, p.Vehicle.RegistrationNo,
        p.Status.ToString(), p.CoverageType.ToString(),
        p.SumInsured, p.Premium, p.BasePremium, p.NcbPercent, p.Deductible,
        p.EffectiveDate, p.ExpiryDate, p.PreviousPolicyId);
}

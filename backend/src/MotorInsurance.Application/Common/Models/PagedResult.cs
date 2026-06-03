namespace MotorInsurance.Application.Common.Models;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public record PolicyDto(
    long Id,
    string PolicyNo,
    long CustomerId,
    string CustomerName,
    long VehicleId,
    string VehicleRegistration,
    string Status,
    string CoverageType,
    decimal SumInsured,
    decimal Premium,
    decimal BasePremium,
    int NcbPercent,
    decimal Deductible,
    DateOnly? EffectiveDate,
    DateOnly? ExpiryDate,
    long? PreviousPolicyId);

public record PolicyHistoryDto(
    string Status,
    decimal Premium,
    DateTime ValidFrom,
    DateTime ValidTo);

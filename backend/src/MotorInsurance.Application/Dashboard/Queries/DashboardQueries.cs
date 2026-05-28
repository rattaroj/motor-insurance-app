using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Application.Dashboard.Queries;

public record DashboardSummaryDto(
    int Customers,
    int Vehicles,
    int Quotations,
    int PoliciesTotal,
    int PoliciesActive,
    int ClaimsOpen,
    int PaymentsPending,
    decimal PaymentsPendingAmount);

public record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public class GetDashboardSummaryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IAppDbContext _db;
    public GetDashboardSummaryHandler(IAppDbContext db) => _db = db;

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery req, CancellationToken ct)
    {
        var pending = _db.Payments.Where(p => p.Status == PaymentStatus.Pending);

        return new DashboardSummaryDto(
            Customers: await _db.Customers.CountAsync(ct),
            Vehicles: await _db.Vehicles.CountAsync(ct),
            Quotations: await _db.Quotations.CountAsync(ct),
            PoliciesTotal: await _db.Policies.CountAsync(ct),
            PoliciesActive: await _db.Policies.CountAsync(p => p.Status == PolicyStatus.Active, ct),
            ClaimsOpen: await _db.Claims.CountAsync(
                c => c.Status != ClaimStatus.Closed && c.Status != ClaimStatus.Rejected, ct),
            PaymentsPending: await pending.CountAsync(ct),
            PaymentsPendingAmount: await pending.SumAsync(p => p.Amount, ct));
    }
}

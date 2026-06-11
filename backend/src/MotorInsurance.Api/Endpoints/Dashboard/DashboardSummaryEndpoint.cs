using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Dashboard;

public record DashboardSummaryDto(
    int Customers,
    int Vehicles,
    int Quotations,
    int PoliciesTotal,
    int PoliciesActive,
    int ClaimsOpen,
    int PaymentsPending,
    decimal PaymentsPendingAmount,
    int InstallmentsOverdue);

/// <summary>GET /api/dashboard/summary — headline counts for the dashboard.</summary>
public class DashboardSummaryEndpoint : EndpointWithoutRequest<DashboardSummaryDto>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public DashboardSummaryEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("dashboard/summary");
        Policies(PermissionPolicy.For(Perms.DashboardRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var pending = _db.Payments.Where(p => p.Status == PaymentStatus.Pending);
        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);

        Response = new DashboardSummaryDto(
            Customers: await _db.Customers.CountAsync(ct),
            Vehicles: await _db.Vehicles.CountAsync(ct),
            Quotations: await _db.Quotations.CountAsync(ct),
            PoliciesTotal: await _db.Policies.CountAsync(ct),
            PoliciesActive: await _db.Policies.CountAsync(p => p.Status == PolicyStatus.Active, ct),
            ClaimsOpen: await _db.Claims.CountAsync(
                c => c.Status != ClaimStatus.Closed && c.Status != ClaimStatus.Rejected, ct),
            PaymentsPending: await pending.CountAsync(ct),
            PaymentsPendingAmount: await pending.SumAsync(p => p.Amount, ct),
            InstallmentsOverdue: await _db.Payments.CountAsync(
                p => p.Direction == PaymentDirection.Inbound
                     && p.Status == PaymentStatus.Pending
                     && p.InstallmentSeq != null
                     && p.DueDate != null
                     && p.DueDate < today, ct));
    }
}

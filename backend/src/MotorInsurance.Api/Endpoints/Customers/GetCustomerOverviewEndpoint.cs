using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Customers;

public record OverviewStats(
    int TotalPolicies, int ActivePolicies, int OpenClaims,
    decimal PremiumPaid, decimal Outstanding);

public record OverviewVehicle(long Id, string RegistrationNo, string Province, string Brand, string Model, int Year);
public record OverviewPolicy(
    long Id, string PolicyNo, string Status, string CoverageType, decimal Premium,
    DateOnly? EffectiveDate, DateOnly? ExpiryDate);
public record OverviewClaim(
    long Id, string ClaimNo, string PolicyNo, string Status, DateOnly IncidentDate,
    decimal ClaimedAmount, decimal? ApprovedAmount);
public record OverviewPayment(
    long Id, string PaymentNo, string Direction, string Status, decimal Amount, DateTime? PaidAt);

/// <summary>The customer header plus every vehicle, policy, claim and payment linked to them.</summary>
public record CustomerOverviewDto(
    long Id, string FullName, string NationalId, string? Phone, string? Email,
    OverviewStats Stats,
    IReadOnlyList<OverviewVehicle> Vehicles,
    IReadOnlyList<OverviewPolicy> Policies,
    IReadOnlyList<OverviewClaim> Claims,
    IReadOnlyList<OverviewPayment> Payments);

/// <summary>
/// GET /api/customers/{id}/overview — the "Customer 360" aggregate: one round-trip that gathers
/// the customer's vehicles, policies, claims and payment trail so the detail page doesn't have to
/// stitch four separate list calls together.
/// </summary>
public class GetCustomerOverviewEndpoint : EndpointWithoutRequest<CustomerOverviewDto>
{
    private readonly IAppDbContext _db;
    public GetCustomerOverviewEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("customers/{id}/overview");
        Policies(PermissionPolicy.For(Perms.CustomerRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");

        var customer = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { c.Id, c.FullName, c.NationalId, c.Phone, c.Email })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Customer), id);

        var vehicles = await _db.Vehicles.AsNoTracking()
            .Where(v => v.CustomerId == id)
            .OrderBy(v => v.RegistrationNo)
            .Select(v => new OverviewVehicle(
                v.Id, v.RegistrationNo, v.Province,
                v.ModelYear.Submodel.Model.Brand.Name, v.ModelYear.Submodel.Model.Name, v.ModelYear.Year))
            .ToListAsync(ct);

        var policies = (await _db.Policies.AsNoTracking()
            .Where(p => p.CustomerId == id)
            .OrderByDescending(p => p.Id)
            .Select(p => new { p.Id, p.PolicyNo, p.Status, p.CoverageType, p.Premium, p.EffectiveDate, p.ExpiryDate })
            .ToListAsync(ct))
            .Select(p => new OverviewPolicy(
                p.Id, p.PolicyNo, p.Status.ToString(), p.CoverageType.ToString(), p.Premium,
                p.EffectiveDate, p.ExpiryDate))
            .ToList();

        var claims = (await _db.Claims.AsNoTracking()
            .Where(c => c.Policy.CustomerId == id)
            .OrderByDescending(c => c.Id)
            .Select(c => new
            {
                c.Id, c.ClaimNo, PolicyNo = c.Policy.PolicyNo, c.Status,
                c.IncidentDate, c.ClaimedAmount, c.ApprovedAmount,
            })
            .ToListAsync(ct))
            .Select(c => new OverviewClaim(
                c.Id, c.ClaimNo, c.PolicyNo, c.Status.ToString(), c.IncidentDate, c.ClaimedAmount, c.ApprovedAmount))
            .ToList();

        // Payment trail for the customer: inbound premiums (via policy) + outbound payouts (via claim).
        var payments = (await _db.Payments.AsNoTracking()
            .Where(p => (p.Policy != null && p.Policy.CustomerId == id)
                || (p.Claim != null && p.Claim.Policy.CustomerId == id))
            .OrderByDescending(p => p.Id)
            .Select(p => new { p.Id, p.PaymentNo, p.Direction, p.Status, p.Amount, p.PaidAt })
            .ToListAsync(ct))
            .Select(p => new OverviewPayment(
                p.Id, p.PaymentNo, p.Direction.ToString(), p.Status.ToString(), p.Amount, p.PaidAt))
            .ToList();

        var stats = new OverviewStats(
            TotalPolicies: policies.Count,
            ActivePolicies: policies.Count(p => p.Status == PolicyStatus.Active.ToString()),
            OpenClaims: claims.Count(c => c.Status != ClaimStatus.Closed.ToString()
                && c.Status != ClaimStatus.Rejected.ToString()),
            PremiumPaid: payments
                .Where(p => p.Direction == PaymentDirection.Inbound.ToString() && p.Status == PaymentStatus.Paid.ToString())
                .Sum(p => p.Amount),
            Outstanding: payments
                .Where(p => p.Direction == PaymentDirection.Inbound.ToString() && p.Status == PaymentStatus.Pending.ToString())
                .Sum(p => p.Amount));

        Response = new CustomerOverviewDto(
            customer.Id, customer.FullName, customer.NationalId, customer.Phone, customer.Email,
            stats, vehicles, policies, claims, payments);
    }
}

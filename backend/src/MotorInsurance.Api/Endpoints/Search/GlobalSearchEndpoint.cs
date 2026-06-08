using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Search;

/// <summary>One search result row. <see cref="Type"/> is "policy" | "claim" | "customer".</summary>
public record SearchHit(string Type, long Id, string Title, string Subtitle);

public record GlobalSearchDto(
    IReadOnlyList<SearchHit> Policies,
    IReadOnlyList<SearchHit> Claims,
    IReadOnlyList<SearchHit> Customers);

/// <summary>
/// GET /api/search?q= — a cross-entity quick-find over policies, claims and customers for the
/// header command box. Each section is included only if the caller has the matching read
/// permission, and is capped so the result stays small.
/// </summary>
public class GlobalSearchEndpoint : EndpointWithoutRequest<GlobalSearchDto>
{
    private const int PerSection = 6;
    private const int MinLength = 2;

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _user;
    public GlobalSearchEndpoint(IAppDbContext db, ICurrentUser user) => (_db, _user) = (db, user);

    public override void Configure()
    {
        Get("search");
        // Any authenticated user may search; each section is gated by its own read permission below.
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var q = (Query<string?>("q", isRequired: false) ?? "").Trim();
        if (q.Length < MinLength)
        {
            Response = new GlobalSearchDto(Array.Empty<SearchHit>(), Array.Empty<SearchHit>(), Array.Empty<SearchHit>());
            return;
        }

        var can = _user.Permissions;

        var policies = can.Contains(Perms.PolicyRead)
            ? await _db.Policies.AsNoTracking()
                .Where(p => p.PolicyNo.Contains(q) || p.Customer.FullName.Contains(q) || p.Vehicle.RegistrationNo.Contains(q))
                .OrderByDescending(p => p.Id).Take(PerSection)
                .Select(p => new SearchHit("policy", p.Id, p.PolicyNo, p.Customer.FullName))
                .ToListAsync(ct)
            : new List<SearchHit>();

        var claims = can.Contains(Perms.ClaimRead)
            ? await _db.Claims.AsNoTracking()
                .Where(c => c.ClaimNo.Contains(q) || c.Policy.PolicyNo.Contains(q))
                .OrderByDescending(c => c.Id).Take(PerSection)
                .Select(c => new SearchHit("claim", c.Id, c.ClaimNo, c.Policy.PolicyNo))
                .ToListAsync(ct)
            : new List<SearchHit>();

        var customers = can.Contains(Perms.CustomerRead)
            ? await _db.Customers.AsNoTracking()
                .Where(c => c.FullName.Contains(q) || c.NationalId.Contains(q)
                    || (c.Phone != null && c.Phone.Contains(q)))
                .OrderBy(c => c.FullName).Take(PerSection)
                .Select(c => new SearchHit("customer", c.Id, c.FullName, c.NationalId))
                .ToListAsync(ct)
            : new List<SearchHit>();

        Response = new GlobalSearchDto(policies, claims, customers);
    }
}

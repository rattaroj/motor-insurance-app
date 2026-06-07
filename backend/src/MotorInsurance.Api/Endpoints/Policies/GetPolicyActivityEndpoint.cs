using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

public record PolicyActivityDto(DateTime At, string Type, string Title, string? Detail, string? User);

/// <summary>
/// GET /api/policies/{id}/activity — a unified, newest-first timeline of everything that happened
/// to a policy: status changes (temporal history), endorsements, payments and notifications.
/// </summary>
public class GetPolicyActivityEndpoint : EndpointWithoutRequest<IReadOnlyList<PolicyActivityDto>>
{
    private readonly IAppDbContext _db;
    private readonly IPolicyHistoryReader _history;
    public GetPolicyActivityEndpoint(IAppDbContext db, IPolicyHistoryReader history)
        => (_db, _history) = (db, history);

    public override void Configure()
    {
        Get("policies/{id}/activity");
        Policies(PermissionPolicy.For(Perms.PolicyRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        if (!await _db.Policies.AsNoTracking().AnyAsync(p => p.Id == id, ct))
            throw new NotFoundException(nameof(Policy), id);

        var items = new List<PolicyActivityDto>();

        // Status changes from the temporal history (each version's period start).
        var history = await _history.GetHistoryAsync(id, ct);
        items.AddRange(history.Select(h => new PolicyActivityDto(
            h.ValidFrom, "status", $"สถานะ: {h.Status}", $"เบี้ย {h.Premium:N2} บาท", null)));

        // Endorsements.
        var endorsements = await _db.Endorsements.AsNoTracking()
            .Where(e => e.PolicyId == id)
            .Select(e => new { e.CreatedAt, e.EndorsementNo, e.FieldName, e.OldValue, e.NewValue, e.CreatedUser })
            .ToListAsync(ct);
        items.AddRange(endorsements.Select(e => new PolicyActivityDto(
            e.CreatedAt, "endorsement", $"สลักหลัง {e.EndorsementNo} ({e.FieldName})",
            $"{e.OldValue ?? "-"} → {e.NewValue ?? "-"}", e.CreatedUser)));

        // Payments.
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.PolicyId == id)
            .Select(p => new { p.CreatedAt, p.PaymentNo, p.Direction, p.Status, p.Amount, p.CreatedUser })
            .ToListAsync(ct);
        items.AddRange(payments.Select(p => new PolicyActivityDto(
            p.CreatedAt, "payment",
            $"{(p.Direction == Domain.Enums.PaymentDirection.Inbound ? "รับเบี้ย" : "จ่ายออก")} {p.PaymentNo}",
            $"{p.Amount:N2} บาท ({p.Status})", p.CreatedUser)));

        // Notifications.
        var notifications = await _db.Notifications.AsNoTracking()
            .Where(n => n.PolicyId == id)
            .Select(n => new { n.SentAt, n.CreatedAt, n.Channel, n.Subject, n.Status })
            .ToListAsync(ct);
        items.AddRange(notifications.Select(n => new PolicyActivityDto(
            n.SentAt ?? n.CreatedAt, "notification", $"แจ้งเตือน ({n.Channel})", $"{n.Subject} — {n.Status}", null)));

        Response = items.OrderByDescending(x => x.At).ToList();
    }
}

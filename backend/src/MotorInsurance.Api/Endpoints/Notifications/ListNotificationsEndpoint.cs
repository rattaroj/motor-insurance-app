using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Notifications;

public record NotificationDto(
    long Id, long? PolicyId, string? PolicyNo, string Channel, string Recipient,
    string Subject, string Body, string Status, DateTime? SentAt, DateTime CreatedAt);

public class ListNotificationsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Channel { get; set; }
    public string? Status { get; set; }
    public long? PolicyId { get; set; }
}

/// <summary>
/// GET /api/notifications — paged history of sent notifications (renewal reminders, etc.),
/// filterable by channel/status/policy + free-text search over policy no/recipient/subject.
/// </summary>
public class ListNotificationsEndpoint : Endpoint<ListNotificationsRequest, PagedResult<NotificationDto>>
{
    private readonly IAppDbContext _db;
    public ListNotificationsEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("notifications");
        Policies(PermissionPolicy.For(Perms.NotificationRead));
    }

    public override async Task HandleAsync(ListNotificationsRequest r, CancellationToken ct)
    {
        var query = _db.Notifications.AsNoTracking().AsQueryable();

        if (r.PolicyId is { } pid)
            query = query.Where(n => n.PolicyId == pid);
        if (!string.IsNullOrWhiteSpace(r.Channel))
            query = query.Where(n => n.Channel == r.Channel);
        if (!string.IsNullOrWhiteSpace(r.Status))
            query = query.Where(n => n.Status == r.Status);

        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var s = r.Search.Trim();
            query = query.Where(n =>
                n.Recipient.Contains(s) ||
                n.Subject.Contains(s) ||
                (n.Policy != null && n.Policy.PolicyNo.Contains(s)));
        }

        Response = await query
            .OrderByDescending(n => n.Id)
            .Select(n => new NotificationDto(
                n.Id, n.PolicyId, n.Policy != null ? n.Policy.PolicyNo : null,
                n.Channel, n.Recipient, n.Subject, n.Body, n.Status, n.SentAt, n.CreatedAt))
            .ToPagedResultAsync(r.Page, r.PageSize, ct);
    }
}

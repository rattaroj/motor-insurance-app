using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Notifications;

public record ResendNotificationResponse(string Status);

/// <summary>
/// POST /api/notifications/{id}/resend — re-dispatch a notification (typically one that failed),
/// updating its status/sent-time in place. Gated by policy.renew (the same actors who remind).
/// </summary>
public class ResendNotificationEndpoint : EndpointWithoutRequest<ResendNotificationResponse>
{
    private readonly IAppDbContext _db;
    private readonly INotificationSender _sender;
    private readonly IDateTimeProvider _clock;
    public ResendNotificationEndpoint(IAppDbContext db, INotificationSender sender, IDateTimeProvider clock)
        => (_db, _sender, _clock) = (db, sender, clock);

    public override void Configure()
    {
        Post("notifications/{id}/resend");
        Policies(PermissionPolicy.For(Perms.PolicyRenew));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var note = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new NotFoundException(nameof(Notification), id);

        var ok = await _sender.SendAsync(
            new NotificationMessage(note.Channel, note.Recipient, note.Subject, note.Body), ct);

        note.Status = ok ? "Sent" : "Failed";
        note.SentAt = ok ? _clock.UtcNow : note.SentAt;
        await _db.SaveChangesAsync(ct);

        Response = new ResendNotificationResponse(note.Status);
    }
}

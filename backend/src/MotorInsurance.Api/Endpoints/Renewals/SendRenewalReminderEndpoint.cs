using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Renewals;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Renewals;

public record SendReminderResponse(long NotificationId, string Channel, string Recipient, string Status);

/// <summary>
/// POST /api/renewals/{policyId}/remind — record + dispatch a renewal reminder to the customer
/// (prefers email, then phone/SMS, else a logged record). Delivery goes through INotificationSender.
/// </summary>
public class SendRenewalReminderEndpoint : EndpointWithoutRequest<SendReminderResponse>
{
    private readonly IAppDbContext _db;
    private readonly INotificationSender _sender;
    private readonly IDateTimeProvider _clock;
    public SendRenewalReminderEndpoint(IAppDbContext db, INotificationSender sender, IDateTimeProvider clock)
        => (_db, _sender, _clock) = (db, sender, clock);

    public override void Configure()
    {
        Post("renewals/{policyId}/remind");
        Policies(PermissionPolicy.For(Perms.PolicyRenew));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var policyId = Route<long>("policyId");

        var p = await _db.Policies.AsNoTracking()
            .Where(x => x.Id == policyId)
            .Select(x => new
            {
                x.PolicyNo,
                x.ExpiryDate,
                Name = x.Customer.FullName,
                x.Customer.Email,
                x.Customer.Phone,
                x.Customer.LineUserId,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Policy), policyId);

        var quote = await RenewalQuote.EstimateAsync(_db, policyId, ct);
        var note = await RenewalReminders.SendAsync(
            _db, _sender, _clock, policyId, p.PolicyNo, p.Name, p.Email, p.Phone, p.ExpiryDate, ct, p.LineUserId,
            quote?.Breakdown.NetPremium);

        Response = new SendReminderResponse(note.Id, note.Channel, note.Recipient, note.Status);
    }
}

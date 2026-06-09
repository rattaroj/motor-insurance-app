using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Renewals;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Renewals;

public record SendBulkRemindersRequest(IReadOnlyList<long> PolicyIds);
public record SendBulkRemindersResponse(int Requested, int Sent, int Failed);

public class SendBulkRemindersValidator : Validator<SendBulkRemindersRequest>
{
    public SendBulkRemindersValidator()
    {
        RuleFor(x => x.PolicyIds).NotEmpty().WithMessage("ต้องเลือกอย่างน้อยหนึ่งกรมธรรม์");
        RuleFor(x => x.PolicyIds.Count).LessThanOrEqualTo(200).WithMessage("ส่งได้สูงสุดครั้งละ 200 กรมธรรม์");
    }
}

/// <summary>
/// POST /api/renewals/remind — dispatch a renewal reminder for each selected policy in one call
/// (the worklist's "remind selected" action). Reuses <see cref="RenewalReminders.SendAsync"/> per
/// policy and returns a sent/failed summary; unknown ids are counted as failed, never throw.
/// </summary>
public class SendBulkRemindersEndpoint : Endpoint<SendBulkRemindersRequest, SendBulkRemindersResponse>
{
    private readonly IAppDbContext _db;
    private readonly INotificationSender _sender;
    private readonly IDateTimeProvider _clock;
    public SendBulkRemindersEndpoint(IAppDbContext db, INotificationSender sender, IDateTimeProvider clock)
        => (_db, _sender, _clock) = (db, sender, clock);

    public override void Configure()
    {
        Post("renewals/remind");
        Policies(PermissionPolicy.For(Perms.PolicyRenew));
    }

    public override async Task HandleAsync(SendBulkRemindersRequest r, CancellationToken ct)
    {
        var ids = r.PolicyIds.Distinct().ToList();

        var policies = await _db.Policies.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.PolicyNo,
                p.ExpiryDate,
                Name = p.Customer.FullName,
                p.Customer.Email,
                p.Customer.Phone,
                p.Customer.LineUserId,
            })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var p in policies)
        {
            var note = await RenewalReminders.SendAsync(
                _db, _sender, _clock, p.Id, p.PolicyNo, p.Name, p.Email, p.Phone, p.ExpiryDate, ct, p.LineUserId);
            if (note.Status == "Sent") sent++;
        }

        // Requested-but-missing policies and dispatch failures both count as failed.
        Response = new SendBulkRemindersResponse(ids.Count, sent, ids.Count - sent);
    }
}

using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public record NotificationTemplateDto(
    string Key, string Label, string Subject, string Body, IReadOnlyList<string> Variables);

/// <summary>Display label + the {{placeholders}} each template supports (shown in the editor).</summary>
public static class NotificationTemplateMeta
{
    public static readonly IReadOnlyDictionary<string, (string Label, string[] Vars)> ByKey =
        new Dictionary<string, (string, string[])>
        {
            ["renewal"] = ("เตือนต่ออายุกรมธรรม์", new[] { "customerName", "policyNo", "expiryDate", "estimatedPremium" }),
            ["installment"] = ("เตือนชำระงวดผ่อนเบี้ย", new[] { "customerName", "policyNo", "installmentSeq", "amount", "dueDate" }),
        };

    public static string Label(string key) => ByKey.TryGetValue(key, out var m) ? m.Label : key;
    public static string[] Vars(string key) => ByKey.TryGetValue(key, out var m) ? m.Vars : System.Array.Empty<string>();
}

/// <summary>GET /api/lookups/notification-templates — all editable templates with their variables.</summary>
public class ListNotificationTemplatesEndpoint : EndpointWithoutRequest<IReadOnlyList<NotificationTemplateDto>>
{
    private readonly IAppDbContext _db;
    public ListNotificationTemplatesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/notification-templates");
        Policies(PermissionPolicy.For(Perms.NotificationManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await _db.NotificationTemplates.AsNoTracking()
            .OrderBy(t => t.TemplateKey)
            .Select(t => new { t.TemplateKey, t.Subject, t.Body })
            .ToListAsync(ct);

        Response = rows.Select(t => new NotificationTemplateDto(
            t.TemplateKey, NotificationTemplateMeta.Label(t.TemplateKey), t.Subject, t.Body,
            NotificationTemplateMeta.Vars(t.TemplateKey))).ToList();
    }
}

public record UpdateNotificationTemplateRequest(string Subject, string Body);

public class UpdateNotificationTemplateValidator : Validator<UpdateNotificationTemplateRequest>
{
    public UpdateNotificationTemplateValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
    }
}

/// <summary>PUT /api/lookups/notification-templates/{key} — edit a template's subject + body.</summary>
public class UpdateNotificationTemplateEndpoint : Endpoint<UpdateNotificationTemplateRequest>
{
    private readonly IAppDbContext _db;
    public UpdateNotificationTemplateEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/notification-templates/{key}");
        Policies(PermissionPolicy.For(Perms.NotificationManage));
    }

    public override async Task HandleAsync(UpdateNotificationTemplateRequest r, CancellationToken ct)
    {
        var key = Route<string>("key")!;
        var tpl = await _db.NotificationTemplates.FirstOrDefaultAsync(t => t.TemplateKey == key, ct)
            ?? throw new NotFoundException(nameof(NotificationTemplate), key);

        tpl.Subject = r.Subject;
        tpl.Body = r.Body;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

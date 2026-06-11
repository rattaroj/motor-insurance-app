using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;

namespace MotorInsurance.Application.Notifications;

/// <summary>
/// Loads editable notification templates by key and substitutes {{placeholder}} tokens.
/// The reminder helpers pass a hardcoded default subject/body, used when the DB row is absent
/// (so notifications keep working even if a template was deleted). Unknown tokens are left as-is.
/// </summary>
public static class NotificationTemplates
{
    private static readonly Regex Token = new(@"\{\{\s*(\w+)\s*\}\}", RegexOptions.Compiled);

    public static string Render(string text, IReadOnlyDictionary<string, string> vars) =>
        Token.Replace(text, m => vars.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    /// <summary>
    /// Resolve a template by key (falling back to the supplied defaults) and render both parts
    /// with <paramref name="vars"/>. Returns the ready-to-send (subject, body).
    /// </summary>
    public static async Task<(string Subject, string Body)> RenderAsync(
        IAppDbContext db, string key, IReadOnlyDictionary<string, string> vars,
        string defaultSubject, string defaultBody, CancellationToken ct = default)
    {
        var tpl = await db.NotificationTemplates.AsNoTracking()
            .Where(t => t.TemplateKey == key)
            .Select(t => new { t.Subject, t.Body })
            .FirstOrDefaultAsync(ct);

        var subject = tpl?.Subject ?? defaultSubject;
        var body = tpl?.Body ?? defaultBody;
        return (Render(subject, vars), Render(body, vars));
    }
}

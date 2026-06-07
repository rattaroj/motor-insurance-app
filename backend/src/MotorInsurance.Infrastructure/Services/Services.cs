using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Common.Models;
using MotorInsurance.Infrastructure.Persistence;

namespace MotorInsurance.Infrastructure;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Document number generator: PREFIX-YYYY-######. The counter lives in the
/// document_counter (prefix, year) table and is incremented atomically: an
/// UPDATE with (UPDLOCK, SERIALIZABLE) reserves the next value, and a range lock
/// serialises the first-use INSERT, so numbers are race-free across concurrent
/// requests and multiple instances while still resetting each year.
/// </summary>
public class DocumentNumberGenerator : IDocumentNumberGenerator
{
    private const string NextValueSql = """
        SET NOCOUNT ON;
        SET XACT_ABORT ON;
        DECLARE @next BIGINT;
        BEGIN TRANSACTION;
            UPDATE document_counter WITH (UPDLOCK, SERIALIZABLE)
                SET @next = next_value, next_value = next_value + 1
                WHERE prefix = @prefix AND [year] = @year;
            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO document_counter (prefix, [year], next_value)
                    VALUES (@prefix, @year, 2);
                SET @next = 1;
            END
        COMMIT TRANSACTION;
        SELECT @next AS Value;
        """;

    private readonly AppDbContext _db;

    public DocumentNumberGenerator(AppDbContext db) => _db = db;

    public async Task<string> NextAsync(string prefix, CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;

        var conn = _db.Database.GetDbConnection();
        var wasClosed = conn.State == ConnectionState.Closed;
        if (wasClosed) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = NextValueSql;
            cmd.Parameters.Add(NamedParam(cmd, "@prefix", prefix));
            cmd.Parameters.Add(NamedParam(cmd, "@year", year));

            var result = await cmd.ExecuteScalarAsync(ct);
            var next = Convert.ToInt64(result);
            return $"{prefix}-{year}-{next:D6}";
        }
        finally
        {
            if (wasClosed) await conn.CloseAsync();
        }
    }

    private static System.Data.Common.DbParameter NamedParam(
        System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        return p;
    }
}

/// <summary>
/// Saves uploaded images to the local filesystem under {webRoot}/uploads/idcards and
/// returns the relative path used to serve them (via UseStaticFiles). Dev-grade storage:
/// for production prefer blob storage + a permission-gated streaming endpoint.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private const long MaxBytes = 5 * 1024 * 1024;   // 5 MB
    private const string RelativeDir = "uploads/idcards";

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
    };

    private readonly string _webRoot;
    public LocalFileStorage(string webRoot) => _webRoot = webRoot;

    public bool IsAllowed(string? contentType) =>
        contentType is not null && Extensions.ContainsKey(contentType);

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        if (!Extensions.TryGetValue(contentType, out var ext))
            throw new ArgumentException($"Unsupported content type '{contentType}'.", nameof(contentType));

        var dir = Path.Combine(_webRoot, "uploads", "idcards");
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fs, ct);
            if (fs.Length > MaxBytes)
            {
                fs.Close();
                File.Delete(fullPath);
                throw new ArgumentException("File exceeds the 5 MB limit.", nameof(content));
            }
        }

        return $"{RelativeDir}/{fileName}";
    }
}

/// <summary>Reads temporal policy history via the SQL Server TemporalAll() API.</summary>
public class PolicyHistoryReader : IPolicyHistoryReader
{
    private readonly AppDbContext _db;
    public PolicyHistoryReader(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PolicyHistoryDto>> GetHistoryAsync(long policyId, CancellationToken ct = default)
    {
        // Project the period columns in SQL, but map Status (a converted enum) in
        // memory so EF doesn't have to translate enum.ToString().
        var rows = await _db.Policies
            .TemporalAll()
            .AsNoTracking()
            .Where(p => p.Id == policyId)
            .OrderBy(p => EF.Property<DateTime>(p, "ValidFrom"))
            .Select(p => new
            {
                p.Status,
                p.Premium,
                ValidFrom = EF.Property<DateTime>(p, "ValidFrom"),
                ValidTo = EF.Property<DateTime>(p, "ValidTo"),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new PolicyHistoryDto(r.Status.ToString(), r.Premium, r.ValidFrom, r.ValidTo))
            .ToList();
    }
}

/// <summary>
/// Reads claim aging via TemporalAll(): for each open claim, finds when it entered its current
/// status (the start of the contiguous tail of history rows sharing that status).
/// </summary>
public class ClaimAgingReader : IClaimAgingReader
{
    private readonly AppDbContext _db;
    public ClaimAgingReader(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ClaimAgingRow>> GetOpenAsync(CancellationToken ct = default)
    {
        var open = await _db.Claims.AsNoTracking()
            .Where(c => c.Status != Domain.Enums.ClaimStatus.Closed)
            .Select(c => new
            {
                c.Id, c.ClaimNo, PolicyNo = c.Policy.PolicyNo, c.Status, c.ClaimedAmount,
            })
            .ToListAsync(ct);
        if (open.Count == 0) return Array.Empty<ClaimAgingRow>();

        var ids = open.Select(o => o.Id).ToList();
        var history = await _db.Claims.TemporalAll().AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Status, ValidFrom = EF.Property<DateTime>(c, "ValidFrom") })
            .ToListAsync(ct);

        var byClaim = history
            .GroupBy(h => h.Id)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.ValidFrom).ToList());

        var result = new List<ClaimAgingRow>(open.Count);
        foreach (var o in open)
        {
            var rows = byClaim.TryGetValue(o.Id, out var l) ? l : new();
            // Walk back from the latest version while the status matches → start of current status.
            var statusSince = rows.Count > 0 ? rows[^1].ValidFrom : DateTime.UtcNow;
            for (var i = rows.Count - 1; i >= 0; i--)
            {
                if (rows[i].Status == o.Status) statusSince = rows[i].ValidFrom;
                else break;
            }
            result.Add(new ClaimAgingRow(o.Id, o.ClaimNo, o.PolicyNo, o.Status, o.ClaimedAmount, statusSince));
        }

        return result;
    }
}

/// <summary>
/// Dev-grade notification sender: logs the message (the caller persists the record in the
/// notification table). Replace with an SMTP/LINE implementation for real delivery.
/// </summary>
public class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> _log;
    public LoggingNotificationSender(ILogger<LoggingNotificationSender> log) => _log = log;

    public Task<bool> SendAsync(NotificationMessage m, CancellationToken ct = default)
    {
        _log.LogInformation("NOTIFY [{Channel}] → {Recipient}: {Subject}", m.Channel, m.Recipient, m.Subject);
        return Task.FromResult(true);
    }
}

/// <summary>Notification delivery settings (section "Notifications"). Channel selects the sender.</summary>
public class NotificationSettings
{
    /// <summary>"Log" (default), "Smtp", or "Line".</summary>
    public string Channel { get; set; } = "Log";
    public SmtpSettings Smtp { get; set; } = new();
    public LineSettings Line { get; set; } = new();
}

public class SmtpSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "noreply@motor.local";
    public bool UseSsl { get; set; }
}

public class LineSettings
{
    /// <summary>LINE Notify bearer token; messages broadcast to that token's target.</summary>
    public string? Token { get; set; }
}

/// <summary>
/// SMTP notification sender (System.Net.Mail — no extra dependency). Only delivers to email
/// recipients (channel "Email"); other channels are skipped (returns false so the caller
/// records Status=Failed). On any SMTP error it logs and returns false rather than throwing.
/// </summary>
public class SmtpNotificationSender : INotificationSender
{
    private readonly SmtpSettings _s;
    private readonly ILogger<SmtpNotificationSender> _log;
    public SmtpNotificationSender(IOptions<NotificationSettings> opt, ILogger<SmtpNotificationSender> log)
        => (_s, _log) = (opt.Value.Smtp, log);

    public async Task<bool> SendAsync(NotificationMessage m, CancellationToken ct = default)
    {
        if (!string.Equals(m.Channel, "Email", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(m.Recipient) || m.Recipient == "-")
        {
            _log.LogWarning("SMTP sender: no email recipient for channel '{Channel}' — skipping.", m.Channel);
            return false;
        }

        try
        {
            using var msg = new MailMessage(_s.From, m.Recipient, m.Subject, m.Body)
            {
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8,
            };
            if (m.AttachmentBytes is { Length: > 0 })
                msg.Attachments.Add(new Attachment(
                    new MemoryStream(m.AttachmentBytes), m.AttachmentName ?? "document.pdf", "application/pdf"));

            using var client = new SmtpClient(_s.Host, _s.Port) { EnableSsl = _s.UseSsl };
            if (!string.IsNullOrEmpty(_s.User))
                client.Credentials = new NetworkCredential(_s.User, _s.Password);

            await client.SendMailAsync(msg, ct);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SMTP send to {Recipient} failed.", m.Recipient);
            return false;
        }
    }
}

/// <summary>
/// LINE notification sender (LINE Notify). Broadcasts the message to the configured token's
/// target; the message Recipient is informational. Logs + returns false on error/missing token.
/// </summary>
public class LineNotificationSender : INotificationSender
{
    private const string NotifyUrl = "https://notify-api.line.me/api/notify";

    private readonly IHttpClientFactory _http;
    private readonly LineSettings _s;
    private readonly ILogger<LineNotificationSender> _log;
    public LineNotificationSender(
        IHttpClientFactory http, IOptions<NotificationSettings> opt, ILogger<LineNotificationSender> log)
        => (_http, _s, _log) = (http, opt.Value.Line, log);

    public async Task<bool> SendAsync(NotificationMessage m, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_s.Token))
        {
            _log.LogWarning("LINE sender: no token configured — skipping.");
            return false;
        }

        try
        {
            var client = _http.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, NotifyUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _s.Token);
            req.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", $"{m.Subject}\n{m.Body}"),
            });

            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                _log.LogWarning("LINE send returned {Status}.", (int)resp.StatusCode);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "LINE send failed.");
            return false;
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
                 ?? "Server=localhost;Database=MotorInsurance;Trusted_Connection=True;TrustServerCertificate=True";

        services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(cs));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDocumentNumberGenerator, DocumentNumberGenerator>();
        services.AddScoped<IPolicyHistoryReader, PolicyHistoryReader>();
        services.AddScoped<IClaimAgingReader, ClaimAgingReader>();

        // Notification delivery: pick the sender from configuration (default = log). The DI seam
        // means call sites never change when switching channels (CLAUDE.md / README note).
        services.Configure<NotificationSettings>(config.GetSection("Notifications"));
        switch (config.GetSection("Notifications")["Channel"]?.Trim().ToLowerInvariant())
        {
            case "smtp":
                services.AddScoped<INotificationSender, SmtpNotificationSender>();
                break;
            case "line":
                services.AddHttpClient();
                services.AddScoped<INotificationSender, LineNotificationSender>();
                break;
            default:
                services.AddScoped<INotificationSender, LoggingNotificationSender>();
                break;
        }

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.Configure<Services.JwtSettings>(config.GetSection("Jwt"));
        services.AddSingleton<IPasswordHasher, Services.Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenService, Services.JwtTokenService>();

        return services;
    }
}

/// <summary>Resolves the current user from the validated JWT claims.</summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _ctx;
    public CurrentUser(IHttpContextAccessor ctx) => _ctx = ctx;

    private ClaimsPrincipal? Principal => _ctx.HttpContext?.User;

    public long? UserId =>
        long.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Username => Principal?.FindFirst("name")?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll("perm").Select(c => c.Value).ToArray() ?? Array.Empty<string>();
}

using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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
        services.AddScoped<INotificationSender, LoggingNotificationSender>();
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

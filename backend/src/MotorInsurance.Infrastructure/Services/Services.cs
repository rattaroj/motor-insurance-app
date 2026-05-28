using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _ctx;
    public CurrentUser(IHttpContextAccessor ctx) => _ctx = ctx;
    public string UserId =>
        _ctx.HttpContext?.User?.Identity?.Name
        ?? _ctx.HttpContext?.Request.Headers["X-User-Id"].FirstOrDefault()
        ?? "system";
}

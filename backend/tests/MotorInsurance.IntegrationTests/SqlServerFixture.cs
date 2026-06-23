using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

namespace MotorInsurance.IntegrationTests;

/// <summary>
/// Spins up a throwaway SQL Server in a container, creates the <c>MotorInsurance</c> database and runs
/// the real Liquibase changelog against it (via the <c>liquibase</c> CLI) so the schema — temporal
/// tables and rowversion columns included — exactly matches production. These are the parts EF InMemory
/// can't model, so this is where the money-path (settle/cancel/suspend + optimistic concurrency) is proven.
///
/// When Docker or the Liquibase CLI is unavailable the fixture degrades gracefully: <see cref="Available"/>
/// is false and the tests are skipped (via <c>[SkippableFact]</c>) rather than failing the whole suite.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DbName = "MotorInsurance";
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public bool Available { get; private set; }
    public string? SkipReason { get; private set; }
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            // Docker not running / image unavailable → skip rather than fail.
            Available = false;
            SkipReason = $"Docker/SQL Server container unavailable: {ex.Message}";
            return;
        }

        // The container's default connection targets master; create our DB then point at it.
        var master = _container.GetConnectionString();
        await ExecuteOnAsync(master, $"IF DB_ID('{DbName}') IS NULL CREATE DATABASE [{DbName}];");

        ConnectionString = new SqlConnectionStringBuilder(master) { InitialCatalog = DbName }.ConnectionString;

        try
        {
            await RunLiquibaseUpdateAsync(master);
        }
        catch (Exception ex) when (ex is not LiquibaseFailedException)
        {
            // Couldn't launch the CLI (not installed / not on PATH) → skip rather than fail.
            Available = false;
            SkipReason = $"Liquibase CLI unavailable: {ex.Message}";
            return;
        }

        Available = true;
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A real <see cref="AppDbContext"/> bound to the container DB (fresh tracking graph).</summary>
    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(options, new TestCurrentUser(), new TestClock());
    }

    // ----- Schema bootstrap (real Liquibase changelog against the container) -----

    private async Task RunLiquibaseUpdateAsync(string masterConnectionString)
    {
        var changelog = LocateChangelog();
        var dbDir = Path.GetDirectoryName(changelog)!;          // liquibase resolves the changelog + ./scripts from here
        var b = new SqlConnectionStringBuilder(masterConnectionString);
        var (host, port) = SplitDataSource(b.DataSource);
        var jdbc = $"jdbc:sqlserver://{host}:{port};databaseName={DbName};encrypt=false;trustServerCertificate=true";

        var psi = new ProcessStartInfo
        {
            FileName = "liquibase",
            WorkingDirectory = dbDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Liquibase 4+ resolves --changeLogFile against its search path (the working dir), not as an
        // absolute path — so run from the db dir with a relative changelog name.
        psi.ArgumentList.Add($"--changeLogFile={Path.GetFileName(changelog)}");
        psi.ArgumentList.Add($"--url={jdbc}");
        psi.ArgumentList.Add("--username=sa");
        psi.ArgumentList.Add($"--password={b.Password}");
        psi.ArgumentList.Add("--headless=true");
        psi.ArgumentList.Add("update");

        // The Windows liquibase.bat parses `java -version` with `find /i` — make sure that resolves to
        // Windows' find.exe even when the test host was launched from a shell (e.g. git-bash) whose
        // PATH shadows it with a Unix `find`. No-op on non-Windows.
        if (OperatingSystem.IsWindows())
            psi.Environment["PATH"] = $"{Environment.SystemDirectory};{psi.Environment["PATH"]}";

        using var proc = Process.Start(psi)!;   // throws Win32Exception if liquibase isn't on PATH
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new LiquibaseFailedException(
                $"liquibase update failed (exit {proc.ExitCode}).\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
    }

    /// <summary>Walks up from the test output dir to find the repo's db/db.changelog-master.xml.</summary>
    private static string LocateChangelog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "db", "db.changelog-master.xml");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate db/db.changelog-master.xml from " + AppContext.BaseDirectory);
    }

    /// <summary>Splits a "host,port" (or "host") SQL Server DataSource into its parts (default port 1433).</summary>
    private static (string Host, string Port) SplitDataSource(string dataSource)
    {
        var ds = dataSource.Replace("tcp:", "", StringComparison.OrdinalIgnoreCase);
        var parts = ds.Split(',', 2);
        return (parts[0], parts.Length > 1 ? parts[1] : "1433");
    }

    private static async Task ExecuteOnAsync(string connectionString, string sql, int timeoutSeconds = 60)
    {
        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = timeoutSeconds;
        await cmd.ExecuteNonQueryAsync();
    }

    // ----- Seed helpers (build a valid FK graph through EF, honouring the enum↔string converters) -----

    /// <summary>
    /// Seeds customer → vehicle (with the brand/model/submodel/year chain) and returns the vehicle's
    /// owner + the vehicle id, so policy/claim seeds can reuse them. <paramref name="tag"/> keeps the
    /// unique columns (national id, registration, names) distinct across tests sharing the one DB.
    /// </summary>
    public async Task<(long CustomerId, long VehicleId)> SeedCustomerAndVehicleAsync(AppDbContext db, string tag)
    {
        var customer = new Customer
        {
            NationalId = Digits13(tag),
            FirstName = "ทดสอบ", LastName = tag, FullName = $"ทดสอบ {tag}",
        };
        db.Customers.Add(customer);

        var brand = new VehicleBrand { Name = $"Brand-{tag}" };
        var model = new VehicleModel { Brand = brand, Name = $"Model-{tag}" };
        var submodel = new VehicleSubmodel { Model = model, Name = $"Sub-{tag}", Powertrain = Powertrain.Gasoline };
        var year = new VehicleModelYear { Submodel = submodel, Year = 2022 };
        var vehicle = new Vehicle
        {
            Customer = customer, RegistrationNo = Truncate($"กก-{tag}", 20),
            Province = "กรุงเทพมหานคร", ModelYear = year,
        };
        db.Vehicles.Add(vehicle);

        await db.SaveChangesAsync();
        return (customer.Id, vehicle.Id);
    }

    /// <summary>Seeds an Issued policy plus its pending inbound premium payment; returns their ids.</summary>
    public async Task<(long PolicyId, long PaymentId)> SeedIssuedPolicyWithPremiumAsync(
        AppDbContext db, string tag, decimal premium = 12_000m)
    {
        var (customerId, vehicleId) = await SeedCustomerAndVehicleAsync(db, tag);

        var policy = new Policy
        {
            PolicyNo = Truncate($"POL-{tag}", 30), CustomerId = customerId, VehicleId = vehicleId,
            Status = PolicyStatus.Issued, CoverageType = CoverageType.Type1,
            SumInsured = 500_000m, Premium = premium, BasePremium = premium,
            EffectiveDate = new DateOnly(2026, 1, 1), ExpiryDate = new DateOnly(2026, 12, 31),
        };
        db.Policies.Add(policy);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            PaymentNo = Truncate($"PAY-{tag}", 30), Direction = PaymentDirection.Inbound,
            Status = PaymentStatus.Pending, PolicyId = policy.Id, Amount = premium,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return (policy.Id, payment.Id);
    }

    /// <summary>Seeds an Active policy, an Approved claim on it, and a pending outbound payout.</summary>
    public async Task<(long PolicyId, long ClaimId, long PaymentId)> SeedApprovedClaimWithPayoutAsync(
        AppDbContext db, string tag, decimal payout = 25_000m)
    {
        var (customerId, vehicleId) = await SeedCustomerAndVehicleAsync(db, tag);

        var policy = new Policy
        {
            PolicyNo = Truncate($"POL-{tag}", 30), CustomerId = customerId, VehicleId = vehicleId,
            Status = PolicyStatus.Active, CoverageType = CoverageType.Type1,
            SumInsured = 500_000m, Premium = 12_000m, BasePremium = 12_000m,
            EffectiveDate = new DateOnly(2026, 1, 1), ExpiryDate = new DateOnly(2026, 12, 31),
        };
        db.Policies.Add(policy);
        await db.SaveChangesAsync();

        var claim = new Claim
        {
            ClaimNo = Truncate($"CLM-{tag}", 30), PolicyId = policy.Id, Status = ClaimStatus.Approved,
            IncidentDate = new DateOnly(2026, 6, 1), ClaimedAmount = 30_000m, ApprovedAmount = payout,
        };
        db.Claims.Add(claim);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            PaymentNo = Truncate($"PAY-{tag}", 30), Direction = PaymentDirection.Outbound,
            Status = PaymentStatus.Pending, ClaimId = claim.Id, Amount = payout,
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        return (policy.Id, claim.Id, payment.Id);
    }

    private static string Digits13(string tag)
    {
        // Deterministic 13-digit "national id" from the tag (stable, unique enough per test).
        var n = (uint)tag.GetHashCode();
        return n.ToString().PadLeft(13, '0')[..13];
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}

/// <summary>Stamps audit user as a fixed test operator.</summary>
internal sealed class TestCurrentUser : ICurrentUser
{
    public long? UserId => 1;
    public string? Username => "integration-test";
    public bool IsAuthenticated => true;
    public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
}

internal sealed class TestClock : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Marks a genuine schema/migration failure (a real bug) vs. an absent CLI (a skip).</summary>
public sealed class LiquibaseFailedException : Exception
{
    public LiquibaseFailedException(string message) : base(message) { }
}

[CollectionDefinition("sqlserver")]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }

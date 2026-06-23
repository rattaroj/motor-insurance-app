using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MotorInsurance.Domain.Enums;
using MotorInsurance.Infrastructure.Persistence;
using Xunit;

namespace MotorInsurance.IntegrationTests;

/// <summary>
/// End-to-end proof of the PromptPay webhook (#7): a gateway callback over real HTTP, against the
/// real API wired to the container database, auto-settles a pending premium and activates the policy.
/// Also covers the secret gate and idempotency on a duplicate callback.
/// </summary>
[Collection("sqlserver")]
public class PromptPayWebhookIntegrationTests : IDisposable
{
    private const string Secret = "integration-webhook-secret";
    private const string Path = "/api/payments/promptpay/webhook";

    private readonly SqlServerFixture _fx;
    private readonly WebApplicationFactory<Program> _app;

    public PromptPayWebhookIntegrationTests(SqlServerFixture fx)
    {
        _fx = fx;
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PromptPay:WebhookSecret"] = Secret,
                ["PolicyLifecycle:Enabled"] = "false",     // don't run the background worker in tests
                ["Notifications:Channel"] = "Log",
            }));
            // AddInfrastructure captures the connection string at build time, before the in-memory
            // config override is merged — so repoint the DbContext at the container DB explicitly.
            b.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(o => o.UseSqlServer(_fx.ConnectionString));
            });
        });
    }

    public void Dispose() => _app.Dispose();

    private HttpClient Client()
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Webhook-Secret", Secret);
        return client;
    }

    [SkippableFact]
    public async Task Webhook_auto_settles_the_premium_and_activates_the_policy()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);
        long policyId;
        string paymentNo;
        await using (var db = _fx.NewContext())
        {
            long paymentId;
            (policyId, paymentId) = await _fx.SeedIssuedPolicyWithPremiumAsync(db, "web1", premium: 9_500m);
            paymentNo = (await db.Payments.FindAsync(paymentId))!.PaymentNo;
        }

        var resp = await Client().PostAsJsonAsync(Path, new
        {
            paymentNo, amount = 9_500m, transactionRef = "GW-TXN-001",
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("settled", await StatusOf(resp));

        await using var verify = _fx.NewContext();
        Assert.Equal(PolicyStatus.Active, (await verify.Policies.FindAsync(policyId))!.Status);
        var paid = await verify.Payments.SingleAsync(p => p.PaymentNo == paymentNo);
        Assert.Equal(PaymentStatus.Paid, paid.Status);
        Assert.Equal("GW-TXN-001", paid.ReferenceNo);
    }

    [SkippableFact]
    public async Task A_duplicate_callback_is_an_idempotent_no_op()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);
        string paymentNo;
        await using (var db = _fx.NewContext())
        {
            var (_, paymentId) = await _fx.SeedIssuedPolicyWithPremiumAsync(db, "web2", premium: 7_000m);
            paymentNo = (await db.Payments.FindAsync(paymentId))!.PaymentNo;
        }

        var body = new { paymentNo, amount = 7_000m, transactionRef = "GW-TXN-002" };
        var first = await Client().PostAsJsonAsync(Path, body);
        var second = await Client().PostAsJsonAsync(Path, body);

        Assert.Equal("settled", await StatusOf(first));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("already_settled", await StatusOf(second));
    }

    [SkippableFact]
    public async Task A_wrong_secret_is_rejected()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Webhook-Secret", "not-the-secret");

        var resp = await client.PostAsJsonAsync(Path, new
        {
            paymentNo = "PAY-nope", amount = 1m, transactionRef = "x",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [SkippableFact]
    public async Task An_amount_that_does_not_match_is_a_conflict()
    {
        Skip.IfNot(_fx.Available, _fx.SkipReason);
        string paymentNo;
        await using (var db = _fx.NewContext())
        {
            var (_, paymentId) = await _fx.SeedIssuedPolicyWithPremiumAsync(db, "web3", premium: 5_000m);
            paymentNo = (await db.Payments.FindAsync(paymentId))!.PaymentNo;
        }

        var resp = await Client().PostAsJsonAsync(Path, new
        {
            paymentNo, amount = 4_000m, transactionRef = "GW-TXN-003",   // wrong amount
        });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    private static async Task<string?> StatusOf(HttpResponseMessage resp)
    {
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
    }
}

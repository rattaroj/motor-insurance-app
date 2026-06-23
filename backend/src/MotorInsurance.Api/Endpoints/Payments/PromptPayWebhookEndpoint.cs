using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Payments;
using MotorInsurance.Domain.Enums;

namespace MotorInsurance.Api.Endpoints.Payments;

/// <summary>
/// The payment-gateway callback for a scanned PromptPay QR. <see cref="PaymentNo"/> is the merchant
/// reference (the payment the QR was generated for), <see cref="Amount"/> the paid amount, and
/// <see cref="TransactionRef"/> the gateway's transaction id (recorded as the settlement reference).
/// </summary>
public record PromptPayWebhookRequest(string PaymentNo, decimal Amount, string TransactionRef);

public record PromptPayWebhookResponse(string Status, long? PaymentId);

public class PromptPayWebhookValidator : Validator<PromptPayWebhookRequest>
{
    public PromptPayWebhookValidator()
    {
        RuleFor(x => x.PaymentNo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.TransactionRef).NotEmpty().MaximumLength(100);
    }
}

/// <summary>
/// POST /api/payments/promptpay/webhook — auto-settles a pending inbound premium when the gateway
/// confirms a PromptPay payment, closing the loop opened by the scan-to-pay QR (no human settle).
/// Anonymous (external callback) but gated by a shared secret in the <c>X-Webhook-Secret</c> header;
/// when <c>PromptPay:WebhookSecret</c> is unset the endpoint is OFF (404), mirroring the token-guarded
/// LINE channel. Idempotent: a retry for an already-settled payment is a 200 no-op, never a double-pay.
/// </summary>
public class PromptPayWebhookEndpoint : Endpoint<PromptPayWebhookRequest, PromptPayWebhookResponse>
{
    private const string SecretHeader = "X-Webhook-Secret";

    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IConfiguration _config;
    public PromptPayWebhookEndpoint(IAppDbContext db, IDateTimeProvider clock, IConfiguration config)
        => (_db, _clock, _config) = (db, clock, config);

    public override void Configure()
    {
        Post("payments/promptpay/webhook");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PromptPayWebhookRequest r, CancellationToken ct)
    {
        var secret = _config["PromptPay:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            // Feature off until a secret is provisioned — don't reveal the endpoint exists.
            await Send.NotFoundAsync(ct);
            return;
        }

        var provided = HttpContext.Request.Headers[SecretHeader].ToString();
        if (!FixedTimeEquals(provided, secret))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var payment = await _db.Payments
            .Include(p => p.Policy)
            .Include(p => p.Claim)
            .Include(p => p.InstallmentPlan).ThenInclude(ip => ip!.Payments)
            .FirstOrDefaultAsync(p => p.PaymentNo == r.PaymentNo, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Payment), r.PaymentNo);

        if (payment.Direction != PaymentDirection.Inbound)
            throw new ConflictException("Webhook พร้อมเพย์ใช้ได้เฉพาะรายการรับเบี้ยเท่านั้น");

        // Already settled (or a duplicate gateway callback) → idempotent no-op, never double-pay.
        if (payment.Status != PaymentStatus.Pending)
        {
            Response = new PromptPayWebhookResponse("already_settled", payment.Id);
            return;
        }

        // Guard against an amount that doesn't match what we billed.
        if (payment.Amount != r.Amount)
            throw new ConflictException(
                $"ยอดชำระ {r.Amount:0.##} ไม่ตรงกับยอดที่เรียกเก็บ {payment.Amount:0.##}");

        PaymentSettlement.Apply(payment, _clock.UtcNow, r.TransactionRef);
        await _db.SaveChangesAsync(ct);

        Response = new PromptPayWebhookResponse("settled", payment.Id);
    }

    /// <summary>Length-aware constant-time comparison so the secret check can't be timed.</summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}

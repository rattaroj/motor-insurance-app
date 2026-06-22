using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Payments;

/// <summary>
/// GET /api/payments/{id}/promptpay-qr — a PromptPay QR PNG for a pending inbound premium,
/// encoding the configured payee and the payment amount, so the customer can scan to pay.
/// </summary>
public class GetPromptPayQrEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly IPromptPayQrGenerator _qr;
    public GetPromptPayQrEndpoint(IAppDbContext db, IPromptPayQrGenerator qr) => (_db, _qr) = (db, qr);

    public override void Configure()
    {
        Get("payments/{id}/promptpay-qr");
        Policies(PermissionPolicy.For(Perms.PaymentRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var payment = await _db.Payments.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { p.PaymentNo, p.Direction, p.Status, p.Amount })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Payment), id);

        if (payment.Direction != PaymentDirection.Inbound || payment.Status != PaymentStatus.Pending)
            throw new ConflictException("QR พร้อมเพย์ใช้ได้เฉพาะรายการรับเบี้ยที่รอชำระเท่านั้น");

        var png = _qr.CreatePng(payment.Amount);
        await Send.BytesAsync(png, $"promptpay-{payment.PaymentNo}.png", contentType: "image/png", cancellation: ct);
    }
}

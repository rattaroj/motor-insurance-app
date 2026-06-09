using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Documents;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using QuestPDF.Fluent;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Payments;

/// <summary>GET /api/payments/{id}/receipt — premium receipt PDF for a paid inbound payment.</summary>
public class GetPaymentReceiptEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public GetPaymentReceiptEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("payments/{id}/receipt");
        Policies(PermissionPolicy.For(Perms.PaymentRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");

        var p = await _db.Payments.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.PaymentNo,
                x.Direction,
                x.Status,
                x.Amount,
                x.PaidAt,
                x.ReferenceNo,
                PolicyNo = x.Policy != null ? x.Policy.PolicyNo : null,
                CustomerName = x.Policy != null ? x.Policy.Customer.FullName : null,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Payment), id);

        if (p.Direction != PaymentDirection.Inbound || p.Status != PaymentStatus.Paid)
            throw new ConflictException("ใบเสร็จออกได้เฉพาะการชำระเบี้ยที่ชำระแล้วเท่านั้น");

        var data = new PremiumReceiptData(
            p.PaymentNo, p.PolicyNo ?? "-", p.CustomerName ?? "-",
            p.Amount, p.PaidAt, p.ReferenceNo, _clock.UtcNow);

        var pdf = new PremiumReceiptDocument(data).GeneratePdf();
        await Send.BytesAsync(pdf, $"{p.PaymentNo}.pdf", contentType: "application/pdf", cancellation: ct);
    }
}

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Documents;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Quotations;
using MotorInsurance.Domain.Entities;
using QuestPDF.Fluent;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Quotations;

/// <summary>Builds the quotation PDF data (re-rates the breakdown from the stored quotation).</summary>
internal static class QuotationPdf
{
    public static async Task<(QuotationDocData Data, string? Email)> BuildAsync(
        IAppDbContext db, IDateTimeProvider clock, long quotationId, CancellationToken ct)
    {
        var q = await db.Quotations.AsNoTracking()
            .Where(x => x.Id == quotationId)
            .Select(x => new
            {
                x.QuotationNo,
                x.CoverageType,
                x.SumInsured,
                x.NcbPercent,
                x.Deductible,
                x.ValidUntil,
                x.VehicleId,
                CustomerName = x.Customer.FullName,
                CustomerEmail = x.Customer.Email,
                Registration = x.Vehicle.RegistrationNo,
                RiderIds = x.Riders.Select(r => r.RiderId).ToList(),
                Riders = x.Riders.Select(r => r.Rider.Name).ToList(),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Quotation), quotationId);

        var b = (await PremiumRatingService.RateAsync(
            db, clock.UtcNow.Year, q.VehicleId, q.CoverageType, q.SumInsured,
            q.NcbPercent, q.Deductible, q.RiderIds, ct)).Breakdown;

        var data = new QuotationDocData(
            q.QuotationNo, q.CustomerName, q.Registration, ThaiLabels.Coverage(q.CoverageType),
            q.SumInsured, q.NcbPercent, q.Deductible, q.Riders,
            b.BasePremium, b.VehicleAgeLoading, b.NcbDiscount, b.DeductibleDiscount, b.RidersTotal, b.NetPremium,
            q.ValidUntil, clock.UtcNow);

        return (data, q.CustomerEmail);
    }
}

/// <summary>GET /api/quotations/{id}/document — the quotation as a PDF.</summary>
public class GetQuotationDocumentEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public GetQuotationDocumentEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("quotations/{id}/document");
        Policies(PermissionPolicy.For(Perms.QuotationRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var (data, _) = await QuotationPdf.BuildAsync(_db, _clock, id, ct);
        var pdf = new QuotationDocument(data).GeneratePdf();
        await Send.BytesAsync(pdf, $"{data.QuotationNo}.pdf", contentType: "application/pdf", cancellation: ct);
    }
}

public record SendQuotationEmailResponse(string Channel, string Recipient, string Status);

/// <summary>POST /api/quotations/{id}/email — email the quotation PDF to the customer.</summary>
public class SendQuotationEmailEndpoint : EndpointWithoutRequest<SendQuotationEmailResponse>
{
    private readonly IAppDbContext _db;
    private readonly INotificationSender _sender;
    private readonly IDateTimeProvider _clock;
    public SendQuotationEmailEndpoint(IAppDbContext db, INotificationSender sender, IDateTimeProvider clock)
        => (_db, _sender, _clock) = (db, sender, clock);

    public override void Configure()
    {
        Post("quotations/{id}/email");
        Policies(PermissionPolicy.For(Perms.QuotationWrite));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var (data, email) = await QuotationPdf.BuildAsync(_db, _clock, id, ct);
        if (string.IsNullOrWhiteSpace(email))
            throw new ConflictException("ลูกค้ายังไม่มีอีเมล ไม่สามารถส่งใบเสนอราคาได้");

        var pdf = new QuotationDocument(data).GeneratePdf();
        var subject = $"ใบเสนอราคา {data.QuotationNo}";
        var body = $"เรียน {data.CustomerName}\nแนบใบเสนอราคาประกันภัยรถยนต์ {data.QuotationNo} " +
                   $"เบี้ยสุทธิ {data.NetPremium:N2} บาท (มีผลถึง {data.ValidUntil:dd/MM/yyyy})";

        var ok = await _sender.SendAsync(
            new NotificationMessage("Email", email!, subject, body, pdf, $"{data.QuotationNo}.pdf"), ct);

        _db.Notifications.Add(new Notification
        {
            PolicyId = null,
            Channel = "Email",
            Recipient = email!,
            Subject = subject,
            Body = body,
            Status = ok ? "Sent" : "Failed",
            SentAt = ok ? _clock.UtcNow : null,
            CreatedAt = _clock.UtcNow,
        });
        await _db.SaveChangesAsync(ct);

        Response = new SendQuotationEmailResponse("Email", email!, ok ? "Sent" : "Failed");
    }
}

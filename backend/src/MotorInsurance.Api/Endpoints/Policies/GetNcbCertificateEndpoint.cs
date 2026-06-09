using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Documents;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Policies;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using QuestPDF.Fluent;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

/// <summary>
/// GET /api/policies/{id}/ncb-certificate — No-Claim-Bonus certificate PDF (หนังสือรับรองประวัติดี),
/// issued for an issued/active/expired policy so the customer can carry their history to another insurer.
/// </summary>
public class GetNcbCertificateEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public GetNcbCertificateEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("policies/{id}/ncb-certificate");
        Policies(PermissionPolicy.For(Perms.PolicyRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");

        var p = await _db.Policies.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.PolicyNo,
                x.Status,
                x.CoverageType,
                x.NcbPercent,
                x.EffectiveDate,
                x.ExpiryDate,
                CustomerName = x.Customer.FullName,
                x.Customer.NationalId,
                VehicleRegistration = x.Vehicle.RegistrationNo,
                VehicleProvince = x.Vehicle.Province,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Policy), id);

        if (p.Status is not (PolicyStatus.Issued or PolicyStatus.Active or PolicyStatus.Expired))
            throw new ConflictException("หนังสือรับรองประวัติดีออกได้เฉพาะกรมธรรม์ที่ออกแล้ว/คุ้มครองอยู่/หมดอายุเท่านั้น");

        var claimFree = await NoClaimBonus.IsClaimFreeAsync(_db, id, ct);

        var data = new NcbCertificateData(
            p.PolicyNo, p.CustomerName, p.NationalId,
            p.VehicleRegistration, p.VehicleProvince,
            p.CoverageType, p.EffectiveDate, p.ExpiryDate,
            p.NcbPercent, claimFree, _clock.UtcNow);

        var pdf = new NcbCertificateDocument(data).GeneratePdf();
        await Send.BytesAsync(pdf, $"NCB-{p.PolicyNo}.pdf", contentType: "application/pdf", cancellation: ct);
    }
}

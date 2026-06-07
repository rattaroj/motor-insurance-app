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

namespace MotorInsurance.Api.Endpoints.Claims;

/// <summary>
/// GET /api/claims/{id}/letter — the claim settlement letter PDF. Available once the claim
/// reaches a decided state (Approved/Rejected/Paid/Closed).
/// </summary>
public class GetClaimLetterEndpoint : EndpointWithoutRequest
{
    private static readonly HashSet<ClaimStatus> Decided = new()
    {
        ClaimStatus.Approved, ClaimStatus.Rejected, ClaimStatus.Paid, ClaimStatus.Closed,
    };

    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public GetClaimLetterEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("claims/{id}/letter");
        Policies(PermissionPolicy.For(Perms.ClaimRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");

        var c = await _db.Claims.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.ClaimNo, x.Status, x.IncidentDate, x.ClaimedAmount, x.ApprovedAmount, x.RejectReason,
                x.SurveyorName,
                PolicyNo = x.Policy.PolicyNo,
                CustomerName = x.Policy.Customer.FullName,
                GarageName = x.Garage != null ? x.Garage.Name : null,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(Claim), id);

        if (!Decided.Contains(c.Status))
            throw new ConflictException("จดหมายแจ้งผลออกได้เฉพาะเคลมที่พิจารณาแล้วเท่านั้น");

        var approved = c.Status is ClaimStatus.Approved or ClaimStatus.Paid or ClaimStatus.Closed
            && c.ApprovedAmount is not null;

        var data = new ClaimLetterData(
            c.ClaimNo, c.PolicyNo, c.CustomerName, ThaiLabels.ClaimStatus(c.Status),
            c.IncidentDate, c.ClaimedAmount, c.ApprovedAmount, c.RejectReason,
            c.GarageName, c.SurveyorName, approved, _clock.UtcNow);

        var pdf = new ClaimLetterDocument(data).GeneratePdf();
        await Send.BytesAsync(pdf, $"{c.ClaimNo}-letter.pdf", contentType: "application/pdf", cancellation: ct);
    }
}

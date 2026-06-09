using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Api.Documents;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using QuestPDF.Fluent;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Payments;

/// <summary>
/// GET /api/policies/{policyId}/installment-schedule — installment-schedule PDF (ตารางผ่อนชำระ)
/// for a policy that is paid in installments. 404 if the policy has no installment plan.
/// </summary>
public class GetInstallmentScheduleEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public GetInstallmentScheduleEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Get("policies/{policyId}/installment-schedule");
        Policies(PermissionPolicy.For(Perms.PaymentRead));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var policyId = Route<long>("policyId");

        var plan = await _db.InstallmentPlans.AsNoTracking()
            .Where(p => p.PolicyId == policyId)
            .Select(p => new
            {
                p.Id,
                p.TotalPremium,
                p.Fee,
                p.Installments,
                p.FrequencyDays,
                p.Status,
                PolicyNo = p.Policy.PolicyNo,
                CustomerName = p.Policy.Customer.FullName,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(nameof(InstallmentPlan), policyId);

        var rows = await _db.Payments.AsNoTracking()
            .Where(x => x.InstallmentPlanId == plan.Id && x.InstallmentSeq != null)
            .OrderBy(x => x.InstallmentSeq)
            .Select(x => new InstallmentRow(x.InstallmentSeq!.Value, x.DueDate, x.Amount, x.Status, x.PaidAt))
            .ToListAsync(ct);

        var data = InstallmentScheduleData.Build(
            plan.PolicyNo, plan.CustomerName,
            plan.TotalPremium, plan.Fee, plan.Installments, plan.FrequencyDays, plan.Status,
            rows, _clock.UtcNow);

        var pdf = new InstallmentScheduleDocument(data).GeneratePdf();
        await Send.BytesAsync(pdf, $"installment-{plan.PolicyNo}.pdf", contentType: "application/pdf", cancellation: ct);
    }
}

using FastEndpoints;
using FluentValidation;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Application.Payments;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Payments;

public record SettlePaymentRequest(string ReferenceNo);

public class SettlePaymentValidator : Validator<SettlePaymentRequest>
{
    public SettlePaymentValidator() => RuleFor(x => x.ReferenceNo).NotEmpty().MaximumLength(100);
}

/// <summary>
/// POST /api/payments/{id}/settle — mark a payment paid. Side effects: inbound premium paid
/// auto-activates an Issued policy; an outbound payout moves its claim Approved -> Paid.
/// </summary>
public class SettlePaymentEndpoint : Endpoint<SettlePaymentRequest>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public SettlePaymentEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Post("payments/{id}/settle");
        Policies(PermissionPolicy.For(Perms.PaymentSettle));
    }

    public override async Task HandleAsync(SettlePaymentRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        await PaymentSettlement.SettleAsync(_db, _clock, id, r.ReferenceNo, ct);
        await Send.NoContentAsync(ct);
    }
}

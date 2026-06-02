using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using MotorInsurance.Domain.Enums;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Policies;

public record CreateEndorsementRequest(
    string? FullName, string? Phone, string? Email, DateOnly EffectiveDate, string? Note);

public record CreateEndorsementResponse(IReadOnlyList<string> EndorsementNos);

public class CreateEndorsementValidator : Validator<CreateEndorsementRequest>
{
    public CreateEndorsementValidator()
    {
        RuleFor(x => x.FullName).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.Email)
            .MaximumLength(255).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x)
            .Must(x => x.FullName is not null || x.Phone is not null || x.Email is not null)
            .WithMessage("At least one customer field (fullName, phone, email) must be provided.");
    }
}

/// <summary>
/// POST /api/policies/{policyId}/endorsements — endorse a policy (สลักหลัง) to change the
/// holder's contact data. Records one endorsement row per changed field (old → new) and
/// applies the change to the customer. Allowed only while the policy is Issued or Active.
/// </summary>
public class CreateEndorsementEndpoint : Endpoint<CreateEndorsementRequest, CreateEndorsementResponse>
{
    private readonly IAppDbContext _db;
    private readonly IDocumentNumberGenerator _docNo;
    private readonly IDateTimeProvider _clock;

    public CreateEndorsementEndpoint(IAppDbContext db, IDocumentNumberGenerator docNo, IDateTimeProvider clock)
        => (_db, _docNo, _clock) = (db, docNo, clock);

    public override void Configure()
    {
        Post("policies/{policyId}/endorsements");
        Policies(PermissionPolicy.For(Perms.PolicyEndorse));
    }

    public override async Task HandleAsync(CreateEndorsementRequest r, CancellationToken ct)
    {
        var policyId = Route<long>("policyId");
        var nos = await EndorseAsync(policyId, r, ct);
        await Send.ResponseAsync(new CreateEndorsementResponse(nos), 201, ct);
    }

    /// <summary>Core logic, separated so it is unit-testable without the HTTP layer.</summary>
    public async Task<IReadOnlyList<string>> EndorseAsync(long policyId, CreateEndorsementRequest r, CancellationToken ct)
    {
        var policy = await _db.Policies.FirstOrDefaultAsync(p => p.Id == policyId, ct)
            ?? throw new NotFoundException(nameof(Policy), policyId);

        if (policy.Status is not (PolicyStatus.Issued or PolicyStatus.Active))
            throw new ConflictException("Only an issued or active policy can be endorsed.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == policy.CustomerId, ct)
            ?? throw new NotFoundException(nameof(Customer), policy.CustomerId);

        var changes = new List<(string Field, string? Old, string? New)>();
        if (r.FullName is not null && r.FullName != customer.FullName)
            changes.Add(("FullName", customer.FullName, r.FullName));
        if (r.Phone is not null && r.Phone != customer.Phone)
            changes.Add(("Phone", customer.Phone, r.Phone));
        if (r.Email is not null && r.Email != customer.Email)
            changes.Add(("Email", customer.Email, r.Email));

        if (changes.Count == 0)
            throw new ConflictException("No changes detected; nothing to endorse.");

        var nos = new List<string>();
        foreach (var (field, oldValue, newValue) in changes)
        {
            var no = await _docNo.NextAsync("END", ct);
            nos.Add(no);
            _db.Endorsements.Add(new Endorsement
            {
                EndorsementNo = no,
                PolicyId = policy.Id,
                FieldName = field,
                OldValue = oldValue,
                NewValue = newValue,
                EffectiveDate = r.EffectiveDate,
                Note = r.Note,
                CreatedAt = _clock.UtcNow,
            });
        }

        // Apply the changes to the customer. A full-name endorsement also re-splits the
        // structured first/last parts so the columns stay consistent.
        if (r.FullName is not null)
        {
            customer.FullName = r.FullName;
            (customer.FirstName, customer.LastName) = Customer.SplitName(r.FullName);
        }
        if (r.Phone is not null) customer.Phone = r.Phone;
        if (r.Email is not null) customer.Email = r.Email;

        await _db.SaveChangesAsync(ct);
        return nos;
    }
}

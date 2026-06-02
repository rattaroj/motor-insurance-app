using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Customers;

public record UpdateCustomerRequest(
    string? Title, string FirstName, string LastName, DateOnly? BirthDate,
    string? Phone, string? Email,
    string? AddressLine, long? ProvinceId, long? DistrictId, long? SubdistrictId, long? PostalCodeId);

public class UpdateCustomerValidator : Validator<UpdateCustomerRequest>
{
    public UpdateCustomerValidator()
    {
        RuleFor(x => x.Title).MaximumLength(20);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BirthDate)
            .Must(d => d!.Value < DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Birth date must be in the past.")
            .When(x => x.BirthDate.HasValue);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.Email)
            .MaximumLength(255).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.AddressLine).MaximumLength(255);
    }
}

/// <summary>
/// PUT /api/customers/{id} — edit a customer's contact data. If the customer already holds
/// a policy, direct edits are blocked: the change must go through a policy endorsement
/// (สลักหลัง). National id is the identity key and is not editable here.
/// </summary>
public class UpdateCustomerEndpoint : Endpoint<UpdateCustomerRequest>
{
    private readonly IAppDbContext _db;
    public UpdateCustomerEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("customers/{id}");
        Policies(PermissionPolicy.For(Perms.CustomerWrite));
    }

    public override async Task HandleAsync(UpdateCustomerRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException(nameof(Customer), id);

        if (await _db.Policies.AnyAsync(p => p.CustomerId == id, ct))
            throw new ConflictException(
                "ลูกค้ารายนี้มีกรมธรรม์แล้ว ต้องแก้ไขข้อมูลผ่านการสลักหลัง " +
                "(POST /api/policies/{policyId}/endorsements).");

        customer.Title = r.Title;
        customer.FirstName = r.FirstName;
        customer.LastName = r.LastName;
        customer.BirthDate = r.BirthDate;
        customer.SyncFullName();
        customer.Phone = r.Phone;
        customer.Email = r.Email;
        customer.AddressLine = r.AddressLine;
        customer.ProvinceId = r.ProvinceId;
        customer.DistrictId = r.DistrictId;
        customer.SubdistrictId = r.SubdistrictId;
        customer.PostalCodeId = r.PostalCodeId;
        await _db.SaveChangesAsync(ct);

        await Send.NoContentAsync(ct);
    }
}

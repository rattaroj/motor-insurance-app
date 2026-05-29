using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Customers;

public record CreateCustomerRequest(string NationalId, string FullName, string? Phone, string? Email);
public record CreateCustomerResponse(long Id);

public class CreateCustomerValidator : Validator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.NationalId).NotEmpty().Length(13);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.Email)
            .MaximumLength(255).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

/// <summary>POST /api/customers.</summary>
public class CreateCustomerEndpoint : Endpoint<CreateCustomerRequest, CreateCustomerResponse>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;
    public CreateCustomerEndpoint(IAppDbContext db, IDateTimeProvider clock) => (_db, _clock) = (db, clock);

    public override void Configure()
    {
        Post("customers");
        Policies(PermissionPolicy.For(Perms.CustomerWrite));
    }

    public override async Task HandleAsync(CreateCustomerRequest r, CancellationToken ct)
    {
        if (await _db.Customers.AnyAsync(c => c.NationalId == r.NationalId, ct))
            throw new ConflictException($"A customer with national id {r.NationalId} already exists.");

        var customer = new Customer
        {
            NationalId = r.NationalId,
            FullName = r.FullName,
            Phone = r.Phone,
            Email = r.Email,
            CreatedAt = _clock.UtcNow,
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        await Send.ResponseAsync(new CreateCustomerResponse(customer.Id), 201, ct);
    }
}

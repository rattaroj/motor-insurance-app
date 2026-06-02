using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Customers;

public record CreateCustomerRequest(
    string NationalId, string? Title, string FirstName, string LastName, DateOnly? BirthDate,
    string? Phone, string? Email,
    string? AddressLine, long? ProvinceId, long? DistrictId, long? SubdistrictId, long? PostalCodeId);
public record CreateCustomerResponse(long Id);

public class CreateCustomerValidator : Validator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.NationalId).NotEmpty().Length(13);
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
            Title = r.Title,
            FirstName = r.FirstName,
            LastName = r.LastName,
            BirthDate = r.BirthDate,
            Phone = r.Phone,
            Email = r.Email,
            AddressLine = r.AddressLine,
            ProvinceId = r.ProvinceId,
            DistrictId = r.DistrictId,
            SubdistrictId = r.SubdistrictId,
            PostalCodeId = r.PostalCodeId,
            CreatedAt = _clock.UtcNow,
        };
        customer.SyncFullName();
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        await Send.ResponseAsync(new CreateCustomerResponse(customer.Id), 201, ct);
    }
}

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;

namespace MotorInsurance.Application.Customers.Commands;

public record CreateCustomerCommand(
    string NationalId,
    string FullName,
    string? Phone = null,
    string? Email = null) : IRequest<long>;

public class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
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

public class CreateCustomerHandler : IRequestHandler<CreateCustomerCommand, long>
{
    private readonly IAppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateCustomerHandler(IAppDbContext db, IDateTimeProvider clock)
        => (_db, _clock) = (db, clock);

    public async Task<long> Handle(CreateCustomerCommand req, CancellationToken ct)
    {
        if (await _db.Customers.AnyAsync(c => c.NationalId == req.NationalId, ct))
            throw new ConflictException($"A customer with national id {req.NationalId} already exists.");

        var customer = new Customer
        {
            NationalId = req.NationalId,
            FullName = req.FullName,
            Phone = req.Phone,
            Email = req.Email,
            CreatedAt = _clock.UtcNow,
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        return customer.Id;
    }
}

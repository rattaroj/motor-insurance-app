using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

public record CreateCustomerTitleRequest(string Name);
public record UpdateCustomerTitleRequest(string Name);

public class CreateCustomerTitleValidator : Validator<CreateCustomerTitleRequest>
{
    public CreateCustomerTitleValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
}

public class UpdateCustomerTitleValidator : Validator<UpdateCustomerTitleRequest>
{
    public UpdateCustomerTitleValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
}

/// <summary>GET /api/lookups/customer-titles.</summary>
public class GetCustomerTitlesEndpoint : EndpointWithoutRequest<IReadOnlyList<OptionDto>>
{
    private readonly IAppDbContext _db;
    public GetCustomerTitlesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/customer-titles");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    // Ordered by Id to preserve the conventional seed order (นาย, นาง, นางสาว).
    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.CustomerTitles.AsNoTracking().OrderBy(t => t.Id)
            .Select(t => new OptionDto(t.Id, t.Name)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/customer-titles.</summary>
public class CreateCustomerTitleEndpoint : Endpoint<CreateCustomerTitleRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreateCustomerTitleEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/customer-titles");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreateCustomerTitleRequest r, CancellationToken ct)
    {
        if (await _db.CustomerTitles.AnyAsync(t => t.Name == r.Name, ct))
            throw new ConflictException($"Title '{r.Name}' already exists.");
        var title = new CustomerTitle { Name = r.Name };
        _db.CustomerTitles.Add(title);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(title.Id);
    }
}

/// <summary>PUT /api/lookups/customer-titles/{id}.</summary>
public class UpdateCustomerTitleEndpoint : Endpoint<UpdateCustomerTitleRequest>
{
    private readonly IAppDbContext _db;
    public UpdateCustomerTitleEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/customer-titles/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdateCustomerTitleRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var title = await _db.CustomerTitles.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException(nameof(CustomerTitle), id);
        if (await _db.CustomerTitles.AnyAsync(t => t.Name == r.Name && t.Id != id, ct))
            throw new ConflictException($"Title '{r.Name}' already exists.");
        title.Name = r.Name;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/customer-titles/{id}.</summary>
public class DeleteCustomerTitleEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeleteCustomerTitleEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/customer-titles/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var title = await _db.CustomerTitles.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException(nameof(CustomerTitle), id);
        // customer.title is a free string (composed into full_name), so "in use" is matched by name.
        if (await _db.Customers.AnyAsync(c => c.Title == title.Name, ct))
            throw new ConflictException("Cannot delete a title that is still in use by a customer.");
        _db.CustomerTitles.Remove(title);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

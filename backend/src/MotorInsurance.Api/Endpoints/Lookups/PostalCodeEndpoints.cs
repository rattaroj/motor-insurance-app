using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Lookups;

// The id of a postal code is its 5-digit code as a number, derived from Code on create.
public record CreatePostalCodeRequest(string Code);
public record UpdatePostalCodeRequest(string Code);

public class CreatePostalCodeValidator : Validator<CreatePostalCodeRequest>
{
    public CreatePostalCodeValidator() => RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{5}$");
}

public class UpdatePostalCodeValidator : Validator<UpdatePostalCodeRequest>
{
    public UpdatePostalCodeValidator() => RuleFor(x => x.Code).NotEmpty().Matches(@"^\d{5}$");
}

/// <summary>GET /api/lookups/postal-codes.</summary>
public class GetPostalCodesEndpoint : EndpointWithoutRequest<IReadOnlyList<PostalCodeOptionDto>>
{
    private readonly IAppDbContext _db;
    public GetPostalCodesEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Get("lookups/postal-codes");
        Policies(PermissionPolicy.For(Perms.LookupRead));
    }

    public override async Task HandleAsync(CancellationToken ct) =>
        Response = await _db.PostalCodes.AsNoTracking().OrderBy(p => p.Code)
            .Select(p => new PostalCodeOptionDto(p.Id, p.Code)).ToListAsync(ct);
}

/// <summary>POST /api/lookups/postal-codes.</summary>
public class CreatePostalCodeEndpoint : Endpoint<CreatePostalCodeRequest, IdResponse>
{
    private readonly IAppDbContext _db;
    public CreatePostalCodeEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Post("lookups/postal-codes");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CreatePostalCodeRequest r, CancellationToken ct)
    {
        var id = long.Parse(r.Code);
        if (await _db.PostalCodes.AnyAsync(p => p.Id == id || p.Code == r.Code, ct))
            throw new ConflictException($"Postal code '{r.Code}' already exists.");
        var postal = new PostalCode { Id = id, Code = r.Code };
        _db.PostalCodes.Add(postal);
        await _db.SaveChangesAsync(ct);
        Response = new IdResponse(postal.Id);
    }
}

/// <summary>PUT /api/lookups/postal-codes/{id}.</summary>
public class UpdatePostalCodeEndpoint : Endpoint<UpdatePostalCodeRequest>
{
    private readonly IAppDbContext _db;
    public UpdatePostalCodeEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Put("lookups/postal-codes/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(UpdatePostalCodeRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        var postal = await _db.PostalCodes.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(PostalCode), id);
        if (await _db.PostalCodes.AnyAsync(p => p.Code == r.Code && p.Id != id, ct))
            throw new ConflictException($"Postal code '{r.Code}' already exists.");
        postal.Code = r.Code;
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>DELETE /api/lookups/postal-codes/{id}.</summary>
public class DeletePostalCodeEndpoint : EndpointWithoutRequest
{
    private readonly IAppDbContext _db;
    public DeletePostalCodeEndpoint(IAppDbContext db) => _db = db;

    public override void Configure()
    {
        Delete("lookups/postal-codes/{id}");
        Policies(PermissionPolicy.For(Perms.LookupManage));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<long>("id");
        var postal = await _db.PostalCodes.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(nameof(PostalCode), id);
        if (await _db.Subdistricts.AnyAsync(s => s.PostalCodeId == id, ct))
            throw new ConflictException("Cannot delete a postal code still referenced by subdistricts.");
        _db.PostalCodes.Remove(postal);
        await _db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

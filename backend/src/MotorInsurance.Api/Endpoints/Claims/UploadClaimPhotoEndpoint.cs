using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using MotorInsurance.Domain.Entities;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Claims;

public class UploadClaimPhotoRequest
{
    public IFormFile File { get; set; } = default!;
}

/// <summary>POST /api/claims/{id}/photos — upload a damage photo and attach it to the claim.</summary>
public class UploadClaimPhotoEndpoint : Endpoint<UploadClaimPhotoRequest, ClaimPhotoDto>
{
    private readonly IAppDbContext _db;
    private readonly IFileStorage _storage;
    private readonly IDateTimeProvider _clock;
    public UploadClaimPhotoEndpoint(IAppDbContext db, IFileStorage storage, IDateTimeProvider clock)
        => (_db, _storage, _clock) = (db, storage, clock);

    public override void Configure()
    {
        Post("claims/{id}/photos");
        AllowFileUploads();
        Policies(PermissionPolicy.For(Perms.ClaimReview));
    }

    public override async Task HandleAsync(UploadClaimPhotoRequest r, CancellationToken ct)
    {
        var id = Route<long>("id");
        if (!await _db.Claims.AnyAsync(c => c.Id == id, ct))
            throw new NotFoundException(nameof(Claim), id);

        if (r.File is null || r.File.Length == 0)
            throw Invalid("A file is required.");
        if (!_storage.IsAllowed(r.File.ContentType))
            throw Invalid("Only JPEG or PNG images are allowed.");

        await using var stream = r.File.OpenReadStream();
        var path = await _storage.SaveAsync(stream, r.File.ContentType, ct);

        var photo = new ClaimPhoto { ClaimId = id, ImagePath = path, CreatedAt = _clock.UtcNow };
        _db.ClaimPhotos.Add(photo);
        await _db.SaveChangesAsync(ct);

        await Send.ResponseAsync(new ClaimPhotoDto(photo.Id, photo.ImagePath, photo.CreatedAt), 201, ct);
    }

    private static ValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = new[] { message } });
}

using FastEndpoints;
using Microsoft.AspNetCore.Http;
using MotorInsurance.Api.Authorization;
using MotorInsurance.Application.Common.Exceptions;
using MotorInsurance.Application.Common.Interfaces;
using Perms = MotorInsurance.Application.Common.Authorization.Permissions;

namespace MotorInsurance.Api.Endpoints.Uploads;

public class UploadIdCardRequest
{
    public IFormFile File { get; set; } = default!;
}

public record UploadIdCardResponse(string Path);

/// <summary>POST /api/uploads/id-card — store a driver's ID-card image, return its relative path.</summary>
public class UploadIdCardEndpoint : Endpoint<UploadIdCardRequest, UploadIdCardResponse>
{
    private readonly IFileStorage _storage;
    public UploadIdCardEndpoint(IFileStorage storage) => _storage = storage;

    public override void Configure()
    {
        Post("uploads/id-card");
        AllowFileUploads();
        Policies(PermissionPolicy.For(Perms.QuotationWrite));
    }

    public override async Task HandleAsync(UploadIdCardRequest r, CancellationToken ct)
    {
        if (r.File is null || r.File.Length == 0)
            throw Invalid("A file is required.");
        if (!_storage.IsAllowed(r.File.ContentType))
            throw Invalid("Only JPEG or PNG images are allowed.");

        await using var stream = r.File.OpenReadStream();
        var path = await _storage.SaveAsync(stream, r.File.ContentType, ct);

        await Send.ResponseAsync(new UploadIdCardResponse(path), 201, ct);
    }

    private static ValidationException Invalid(string message) =>
        new(new Dictionary<string, string[]> { ["file"] = new[] { message } });
}

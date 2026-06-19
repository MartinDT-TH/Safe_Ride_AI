using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.IdentityVerification.DTOs;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.ExternalServices;

namespace SafeRide.Infrastructure.Services;

public sealed class CloudinaryIdentityDocumentStorage : IIdentityDocumentStorage
{
    private static readonly HashSet<string> ImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png"
    };

    private readonly CloudinaryOptions _options;

    public CloudinaryIdentityDocumentStorage(IOptions<CloudinaryOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredIdentityDocumentFile> SaveAsync(
        Guid driverId,
        KycDocumentType documentType,
        string slot,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var publicId = BuildPublicId(driverId, documentType, slot, originalFileName, contentType);
        var cloudinary = new Cloudinary(new Account(
            _options.CloudName,
            _options.ApiKey,
            _options.ApiSecret));

        var upload = ImageContentTypes.Contains(contentType)
            ? await UploadImageAsync(
                cloudinary,
                publicId,
                originalFileName,
                content,
                cancellationToken)
            : await UploadRawAsync(
                cloudinary,
                publicId,
                originalFileName,
                content,
                cancellationToken);

        if (upload.Error != null || upload.SecureUrl == null)
        {
            throw new InvalidOperationException(
                upload.Error?.Message ?? "Cloudinary did not return a document URL.");
        }

        return new StoredIdentityDocumentFile(
            upload.SecureUrl.ToString(),
            originalFileName,
            contentType,
            upload.Bytes);
    }

    private static async Task<RawUploadResult> UploadImageAsync(
        Cloudinary cloudinary,
        string publicId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        return await cloudinary.UploadAsync(
            new ImageUploadParams
            {
                File = new FileDescription(fileName, content),
                PublicId = publicId,
                Overwrite = false,
                Invalidate = true,
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            },
            cancellationToken);
    }

    private static async Task<RawUploadResult> UploadRawAsync(
        Cloudinary cloudinary,
        string publicId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        return await cloudinary.UploadAsync(
            new RawUploadParams
            {
                File = new FileDescription(fileName, content),
                PublicId = publicId,
                Overwrite = false,
                Invalidate = true
            },
            "raw",
            cancellationToken);
    }

    private string BuildPublicId(
        Guid driverId,
        KycDocumentType documentType,
        string slot,
        string originalFileName,
        string contentType)
    {
        var safeSlot = NormalizeSegment(slot);
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
        var uniqueName = $"{safeSlot}-{timestamp}-{Guid.NewGuid():N}";

        if (!ImageContentTypes.Contains(contentType))
        {
            uniqueName += Path.GetExtension(originalFileName).ToLowerInvariant();
        }

        return $"saferide/identity-verification/{driverId:N}/{documentType.ToString().ToLowerInvariant()}/{uniqueName}";
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.CloudName) ||
            _options.CloudName == "YOUR_CLOUD_NAME" ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new InvalidOperationException(
                "Cloudinary configuration is incomplete.");
        }
    }

    private static string NormalizeSegment(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "front" => "front",
            "back" => "back",
            "file" => "file",
            _ => "document"
        };
    }
}

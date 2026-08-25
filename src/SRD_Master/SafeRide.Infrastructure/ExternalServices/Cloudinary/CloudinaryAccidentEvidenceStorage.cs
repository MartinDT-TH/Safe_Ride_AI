using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.ExternalServices.Cloudinary;

public sealed class CloudinaryAccidentEvidenceStorage : IAccidentEvidenceStorage
{
    private readonly CloudinaryOptions _options;

    public CloudinaryAccidentEvidenceStorage(IOptions<CloudinaryOptions> options) =>
        _options = options.Value;

    public async Task<StoredAccidentEvidenceFile> SaveAsync(
        long accidentId,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var publicId = $"saferide/accident-evidence/{accidentId}/{Guid.NewGuid():N}";
        RawUploadResult upload;
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            upload = await client.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(originalFileName, content),
                PublicId = publicId,
                Overwrite = false,
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            }, cancellationToken);
        }
        else
        {
            upload = await client.UploadAsync(new RawUploadParams
                {
                    File = new FileDescription(originalFileName, content),
                    PublicId = publicId,
                    Overwrite = false,
                    Invalidate = true
                },
                "raw",
                cancellationToken);
        }
        if (upload.Error is not null || upload.SecureUrl is null)
            throw new InvalidOperationException(
                upload.Error?.Message ?? "Cloudinary did not return an evidence URL.");
        return new StoredAccidentEvidenceFile(
            upload.SecureUrl.ToString(), upload.PublicId, fileSizeBytes);
    }

    public async Task DeleteAsync(
        string publicId,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return;
        var client = CreateClient();
        var resourceType = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? ResourceType.Image
            : ResourceType.Raw;
        await client.DestroyAsync(new DeletionParams(publicId)
        {
            ResourceType = resourceType,
            Invalidate = true
        });
        cancellationToken.ThrowIfCancellationRequested();
    }

    private global::CloudinaryDotNet.Cloudinary CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.CloudName)
            || _options.CloudName == "YOUR_CLOUD_NAME"
            || string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.ApiSecret))
            throw new InvalidOperationException("Cloudinary configuration is incomplete.");
        return new global::CloudinaryDotNet.Cloudinary(new Account(
            _options.CloudName, _options.ApiKey, _options.ApiSecret));
    }
}

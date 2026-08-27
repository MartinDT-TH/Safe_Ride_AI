using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.RiskProtection;

namespace SafeRide.Infrastructure.ExternalServices.Cloudinary;

public sealed class CloudinarySafetyTerminationEvidenceStorage : ISafetyTerminationEvidenceStorage
{
    private readonly CloudinaryOptions _options;

    public CloudinarySafetyTerminationEvidenceStorage(IOptions<CloudinaryOptions> options) => _options = options.Value;

    public async Task<StoredSafetyTerminationEvidence> SaveAsync(
        long tripId, string originalFileName, string contentType, long fileSizeBytes,
        Stream content, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var publicId = $"saferide/safety-terminations/{tripId}/{Guid.NewGuid():N}";
        RawUploadResult upload = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? await client.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(originalFileName, content), PublicId = publicId,
                Overwrite = false, Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            }, cancellationToken)
            : await client.UploadAsync(new RawUploadParams
            {
                File = new FileDescription(originalFileName, content), PublicId = publicId,
                Overwrite = false, Invalidate = true
            }, "raw", cancellationToken);
        if (upload.Error is not null || upload.SecureUrl is null)
            throw new InvalidOperationException(upload.Error?.Message ?? "Cloudinary did not return a safety evidence URL.");
        return new StoredSafetyTerminationEvidence(
            upload.SecureUrl.ToString(), upload.PublicId, originalFileName, contentType, fileSizeBytes);
    }

    public async Task DeleteAsync(string publicId, string contentType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return;
        await CreateClient().DestroyAsync(new DeletionParams(publicId)
        {
            ResourceType = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? ResourceType.Image : ResourceType.Raw,
            Invalidate = true
        });
        cancellationToken.ThrowIfCancellationRequested();
    }

    private global::CloudinaryDotNet.Cloudinary CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.CloudName) || _options.CloudName == "YOUR_CLOUD_NAME"
            || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ApiSecret))
            throw new InvalidOperationException("Cloudinary configuration is incomplete.");
        return new global::CloudinaryDotNet.Cloudinary(new Account(
            _options.CloudName, _options.ApiKey, _options.ApiSecret));
    }
}

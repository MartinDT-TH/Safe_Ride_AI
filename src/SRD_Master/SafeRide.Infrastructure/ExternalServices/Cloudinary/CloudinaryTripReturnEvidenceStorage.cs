using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.ExternalServices.Cloudinary;

/// <summary>
/// Uploads driver-substitute return evidence photos to Cloudinary.
/// Public ID path: saferide/trip-return-evidence/{tripId}/photo-{displayOrder}
/// One confirmation per trip is enforced in the service layer, so this path is unique.
/// </summary>
public sealed class CloudinaryTripReturnEvidenceStorage : ITripReturnEvidenceStorage
{
    private readonly CloudinaryOptions _options;

    public CloudinaryTripReturnEvidenceStorage(IOptions<CloudinaryOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredReturnEvidenceFile> SaveAsync(
        long tripId,
        int displayOrder,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.CloudName) ||
            _options.CloudName == "YOUR_CLOUD_NAME" ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new InvalidOperationException("Cloudinary configuration is incomplete.");
        }

        var cloudinary = new global::CloudinaryDotNet.Cloudinary(new Account(
            _options.CloudName,
            _options.ApiKey,
            _options.ApiSecret));

        var publicId = $"saferide/trip-return-evidence/{tripId}/photo-{displayOrder}";

        var upload = await cloudinary.UploadAsync(
            new ImageUploadParams
            {
                File = new FileDescription(originalFileName, content),
                PublicId = publicId,
                Overwrite = true,
                Invalidate = true,
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            },
            cancellationToken);

        if (upload.Error != null || upload.SecureUrl == null)
        {
            throw new InvalidOperationException(
                upload.Error?.Message ?? "Cloudinary did not return an image URL.");
        }

        // Content length after upload; stream.Length may be unavailable for forward-only streams,
        // so we fall back to the size reported by the upload params.
        long? sizeBytes = null;
        try { sizeBytes = content.Length; }
        catch (NotSupportedException) { }

        return new StoredReturnEvidenceFile(
            upload.SecureUrl.ToString(),
            upload.PublicId,
            originalFileName,
            contentType,
            sizeBytes);
    }
}

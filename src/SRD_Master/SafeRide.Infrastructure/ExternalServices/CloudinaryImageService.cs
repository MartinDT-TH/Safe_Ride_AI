using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;

namespace SafeRide.Infrastructure.ExternalServices;

public sealed class CloudinaryImageService : ICloudinaryImageService
{
    private readonly CloudinaryOptions _options;

    public CloudinaryImageService(IOptions<CloudinaryOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> UploadAvatarAsync(
        Guid userId,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.CloudName) ||
            _options.CloudName == "YOUR_CLOUD_NAME" ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new InvalidOperationException(
                "Cloudinary configuration is incomplete.");
        }

        var cloudinary = new Cloudinary(new Account(
            _options.CloudName,
            _options.ApiKey,
            _options.ApiSecret));
        var upload = await cloudinary.UploadAsync(
            new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = $"saferide/avatars/{userId:N}",
                Overwrite = true,
                Invalidate = true,
                Transformation = new Transformation()
                    .Width(512)
                    .Height(512)
                    .Crop("fill")
                    .Gravity("face")
                    .Quality("auto")
                    .FetchFormat("auto")
            },
            cancellationToken);

        if (upload.Error != null || upload.SecureUrl == null)
        {
            throw new InvalidOperationException(
                upload.Error?.Message ?? "Cloudinary did not return an image URL.");
        }

        return upload.SecureUrl.ToString();
    }
}

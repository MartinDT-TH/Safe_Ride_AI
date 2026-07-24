using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SafeRide.Application.Features.Bookings;

namespace SafeRide.Infrastructure.ExternalServices.Cloudinary;

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

        var cloudinary = new global::CloudinaryDotNet.Cloudinary(new Account(
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

    public async Task<string> UploadTripChatImageAsync(
        long tripId,
        Stream stream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            throw new BookingException(
                "trip_chat.cloudinary_not_configured",
                "Cloudinary chưa được cấu hình.",
                503);
        }

        var extension = contentType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => throw new InvalidOperationException("Định dạng ảnh không được hỗ trợ.")
        };
        var publicId = Guid.NewGuid().ToString("N");
        var cloudinary = new global::CloudinaryDotNet.Cloudinary(new Account(
            _options.CloudName,
            _options.ApiKey,
            _options.ApiSecret));
        var upload = await cloudinary.UploadAsync(
            new ImageUploadParams
            {
                File = new FileDescription($"{publicId}{extension}", stream),
                Folder = $"saferide/trip-chat/{tripId}",
                PublicId = publicId,
                Overwrite = false,
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            },
            cancellationToken);

        if (upload.Error != null || upload.SecureUrl == null)
        {
            throw new InvalidOperationException(
                upload.Error?.Message ?? "Cloudinary không trả về URL ảnh.");
        }

        return upload.SecureUrl.ToString();
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.CloudName)
            && _options.CloudName != "YOUR_CLOUD_NAME"
            && !string.IsNullOrWhiteSpace(_options.ApiKey)
            && !string.IsNullOrWhiteSpace(_options.ApiSecret);
    }
}

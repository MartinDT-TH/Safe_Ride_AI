namespace SafeRide.Infrastructure.ExternalServices.Cloudinary;

public interface ICloudinaryImageService
{
    Task<string> UploadAvatarAsync(
        Guid userId,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default);
}

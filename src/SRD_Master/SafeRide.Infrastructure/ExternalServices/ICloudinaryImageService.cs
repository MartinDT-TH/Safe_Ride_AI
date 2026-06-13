namespace SafeRide.Infrastructure.ExternalServices;

public interface ICloudinaryImageService
{
    Task<string> UploadAvatarAsync(
        Guid userId,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default);
}

namespace SafeRide.Infrastructure.ExternalServices.Cloudinary;

public interface ICloudinaryImageService
{
    Task<string> UploadAvatarAsync(
        Guid userId,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<string> UploadTripChatImageAsync(
        long tripId,
        Stream stream,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<CloudinaryAudioUpload> UploadAiChatAudioAsync(
        Guid userId,
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default);

    Task DeleteAiChatAudioAsync(
        string publicId,
        CancellationToken cancellationToken = default);
}

public sealed record CloudinaryAudioUpload(string Url, string PublicId);

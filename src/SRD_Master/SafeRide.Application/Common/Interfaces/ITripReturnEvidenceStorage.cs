namespace SafeRide.Application.Common.Interfaces;

/// <summary>
/// Stores evidence photo files for driver-substitute return confirmations.
/// The implementation decides the storage backend (Cloudinary, blob, etc.).
/// </summary>
public interface ITripReturnEvidenceStorage
{
    Task<StoredReturnEvidenceFile> SaveAsync(
        long tripId,
        int displayOrder,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result of a single evidence photo upload.
/// </summary>
public sealed record StoredReturnEvidenceFile(
    string ImageUrl,
    string? ImagePublicId,
    string? OriginalFileName,
    string? ContentType,
    long? FileSizeBytes);

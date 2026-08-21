namespace SafeRide.Application.Common.Interfaces;

public interface IEvidenceFileValidator
{
    Task<ValidatedEvidenceFile> ValidateAsync(
        EvidenceFileValidationRequest request,
        CancellationToken cancellationToken);
}

public sealed record EvidenceFileValidationRequest(
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Stream Content,
    IReadOnlyCollection<string> AllowedContentTypes,
    long MaxFileSizeBytes,
    EvidenceFileValidationErrorCodes ErrorCodes);

public sealed record EvidenceFileValidationErrorCodes(
    string Invalid,
    string MalwareDetected,
    string ScannerUnavailable);

public sealed record ValidatedEvidenceFile(
    string FileName,
    string ContentType,
    long FileSizeBytes,
    MemoryStream Content);

namespace SafeRide.Application.Features.IdentityVerification.DTOs;

public sealed record StoredIdentityDocumentFile(
    string Url,
    string FileName,
    string ContentType,
    long SizeBytes);


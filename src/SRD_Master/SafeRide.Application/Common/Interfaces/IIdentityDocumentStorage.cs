using SafeRide.Application.Features.IdentityVerification.DTOs;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface IIdentityDocumentStorage
{
    Task<StoredIdentityDocumentFile> SaveAsync(
        Guid driverId,
        KycDocumentType documentType,
        string slot,
        string originalFileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);
}


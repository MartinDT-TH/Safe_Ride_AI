using SafeRide.Application.Features.AdminDrivers;
using SafeRide.Application.Features.AdminDrivers.Queries.GetAdminDrivers;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface IAdminDriverAccountService
{
    Task<GetAdminDriversResult> GetDriversAsync(
        string status,
        CancellationToken cancellationToken);

    Task<AdminDriverResponse> BlockDriverAsync(
        Guid driverId,
        string? reason,
        CancellationToken cancellationToken);

    Task<AdminDriverResponse> UnlockDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken);

    Task<AdminDriverResponse> ReviewKycAsync(
        Guid driverId,
        KycStatus status,
        string? rejectionReason,
        CancellationToken cancellationToken);
}

using SafeRide.Application.Common.Models;

namespace SafeRide.Application.Common.Interfaces;

public interface IDriverRealtimeService
{
    Task UpdateDriverLocationAsync(
        Guid driverId,
        DriverLocationUpdateInput location,
        CancellationToken cancellationToken = default);

    Task UpdateDriverLocationAsync(
        Guid driverId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);

    Task SetDriverOnlineAsync(
        Guid driverId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default);

    Task SetDriverOfflineAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);

    Task RemoveDriverFromOnlineGeoAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);
}

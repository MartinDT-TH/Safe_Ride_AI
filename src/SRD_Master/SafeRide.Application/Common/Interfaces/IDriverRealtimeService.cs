namespace SafeRide.Application.Common.Interfaces;

public interface IDriverRealtimeService
{
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

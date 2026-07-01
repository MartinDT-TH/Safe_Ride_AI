using SafeRide.Application.Features.Drivers.DTOs;
using SafeRide.Contracts.Responses.Drivers;

namespace SafeRide.Application.Common.Interfaces;

public interface IDriverQueryService
{
    Task<IReadOnlyList<NearbyDriverResponse>> GetNearbyDriversAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken);

    Task<ActiveDriverTripDto?> GetActiveTripAsync(
        Guid driverId,
        CancellationToken cancellationToken);

    Task<bool> HasActiveTripOrBusyStatusAsync(
        Guid driverId,
        CancellationToken cancellationToken);
}

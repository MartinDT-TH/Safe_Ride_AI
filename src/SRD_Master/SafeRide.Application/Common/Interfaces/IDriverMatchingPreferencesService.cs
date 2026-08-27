using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Common.Interfaces;

public interface IDriverMatchingPreferencesService
{
    Task<DriverMatchingPreferencesDto> GetAsync(
        Guid driverId,
        CancellationToken cancellationToken);

    Task<DriverMatchingPreferencesDto> UpdateAsync(
        Guid driverId,
        bool acceptLongPickupTrips,
        bool acceptLongDistanceTrips,
        CancellationToken cancellationToken);
}

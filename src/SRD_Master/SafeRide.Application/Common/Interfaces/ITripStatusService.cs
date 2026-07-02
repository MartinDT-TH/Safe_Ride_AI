using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface ITripStatusService
{
    Task UpdateDriverTripStatusAsync(
        Guid driverId,
        long tripId,
        TripStatus tripStatus,
        CancellationToken cancellationToken);

    Task EndTripAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken);

    Task CompleteTripAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken);
}

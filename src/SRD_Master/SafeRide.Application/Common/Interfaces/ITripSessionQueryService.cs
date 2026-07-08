using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public sealed record TripSessionInfo(
    long TripId,
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    TripStatus TripStatus,
    DateTime CreatedAt,
    DateTime? DriverAssignedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public interface ITripSessionQueryService
{
    Task<TripSessionInfo?> GetActiveTripForUserAsync(
        Guid userId,
        DateTime? existedAtOrBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<TripSessionInfo?> GetTripForUserAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken = default);

    Task<TripSessionInfo?> GetTripForBookingForUserAsync(
        Guid userId,
        long bookingId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveTripForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveTripForDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);
}

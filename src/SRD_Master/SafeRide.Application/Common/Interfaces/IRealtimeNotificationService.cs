using SafeRide.Application.Common.Realtime;

namespace SafeRide.Application.Common.Interfaces;

public interface IRealtimeNotificationService
{
    Task PublishBookingStatusChangedAsync(
        BookingStatusChangedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishTripCreatedAsync(
        TripCreatedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishTripStatusChangedAsync(
        TripStatusChangedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishDriverLocationUpdatedAsync(
        DriverLocationUpdatedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishDriverOfferCreatedAsync(
        DriverOfferCreatedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishDriverOfferRejectedAsync(
        DriverOfferRejectedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishDriverMatchedAsync(
        DriverMatchedEvent notification,
        CancellationToken cancellationToken = default);
}

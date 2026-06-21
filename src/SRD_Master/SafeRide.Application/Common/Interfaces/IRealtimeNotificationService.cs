using SafeRide.Application.Common.Realtime;

namespace SafeRide.Application.Common.Interfaces;

public interface IRealtimeNotificationService
{
    Task PublishBookingStatusChangedAsync(
        BookingStatusChangedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishBookingSearchingStartedAsync(
        BookingSearchingStartedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishTripCreatedAsync(
        TripCreatedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishBookingDriverAssignedAsync(
        BookingDriverAssignedEvent notification,
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

    Task PublishDriverOfferReceivedAsync(
        DriverOfferReceivedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishDriverOfferRejectedAsync(
        DriverOfferRejectedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishDriverOfferAcceptedAsync(
        DriverOfferAcceptedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishDriverOfferExpiredAsync(
        DriverOfferExpiredEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishDriverOfferCancelledAsync(
        DriverOfferCancelledEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishDriverMatchedAsync(
        DriverMatchedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishCustomerConfirmedDriverOfferAsync(
        CustomerConfirmedDriverOfferEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishBookingSearchRadiusExpandedAsync(
        BookingSearchRadiusExpandedEvent notification,
        CancellationToken cancellationToken = default);

    Task PublishBookingExpiredAsync(
        BookingExpiredEvent notification,
        CancellationToken cancellationToken = default);
}

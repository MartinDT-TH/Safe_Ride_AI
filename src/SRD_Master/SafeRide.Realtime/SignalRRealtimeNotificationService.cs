using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;

namespace SafeRide.Realtime;

public sealed class SignalRRealtimeNotificationService
    : IRealtimeNotificationService
{
    private readonly IHubContext<SafeRideHub> _hubContext;

    public SignalRRealtimeNotificationService(
        IHubContext<SafeRideHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishBookingStatusChangedAsync(
        BookingStatusChangedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "BookingStatusChanged",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "BookingStatusChanged",
                notification,
                cancellationToken));
    }

    public Task PublishTripCreatedAsync(
        TripCreatedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "TripCreated",
                notification,
                cancellationToken),
            SendToDriverAsync(
                notification.DriverId,
                "TripCreated",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "TripCreated",
                notification,
                cancellationToken),
            SendToTripAsync(
                notification.TripId,
                "TripCreated",
                notification,
                cancellationToken));
    }

    public Task PublishTripStatusChangedAsync(
        TripStatusChangedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "TripStatusChanged",
                notification,
                cancellationToken),
            SendToDriverAsync(
                notification.DriverId,
                "TripStatusChanged",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "TripStatusChanged",
                notification,
                cancellationToken),
            SendToTripAsync(
                notification.TripId,
                "TripStatusChanged",
                notification,
                cancellationToken));
    }

    public Task PublishDriverLocationUpdatedAsync(
        DriverLocationUpdatedEvent notification,
        CancellationToken cancellationToken = default)
    {
        if (!notification.TripId.HasValue)
        {
            return SendToDriverAsync(
                notification.DriverId,
                "DriverLocationUpdated",
                notification,
                cancellationToken);
        }

        return SendToTripAsync(
            notification.TripId.Value,
            "DriverLocationUpdated",
            notification,
            cancellationToken);
    }

    public Task PublishDriverOfferCreatedAsync(
        DriverOfferCreatedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "DriverOfferCreated",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "DriverOfferCreated",
                notification,
                cancellationToken));
    }

    public Task PublishBookingSearchingStartedAsync(
        BookingSearchingStartedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "BookingSearchingStarted",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "BookingSearchingStarted",
                notification,
                cancellationToken));
    }

    public Task PublishBookingDriverAssignedAsync(
        BookingDriverAssignedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "BookingDriverAssigned",
                notification,
                cancellationToken),
            SendToDriverAsync(
                notification.DriverId,
                "BookingDriverAssigned",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "BookingDriverAssigned",
                notification,
                cancellationToken),
            SendToTripAsync(
                notification.TripId,
                "BookingDriverAssigned",
                notification,
                cancellationToken));
    }

    public Task PublishDriverOfferReceivedAsync(
        DriverOfferReceivedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return SendToDriverAsync(
            notification.DriverId,
            "ReceiveDriverOffer",
            notification,
            cancellationToken);
    }

    public Task PublishDriverOfferRejectedAsync(
        DriverOfferRejectedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "DriverOfferRejected",
                notification,
                cancellationToken),
            SendToDriverAsync(
                notification.DriverId,
                "DriverOfferRejected",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "DriverOfferRejected",
                notification,
                cancellationToken));
    }

    public Task PublishDriverOfferAcceptedAsync(
        DriverOfferAcceptedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "DriverOfferAccepted",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "DriverOfferAccepted",
                notification,
                cancellationToken));
    }

    public Task PublishDriverOfferExpiredAsync(
        DriverOfferExpiredEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "DriverOfferExpired",
                notification,
                cancellationToken),
            SendToDriverAsync(
                notification.DriverId,
                "DriverOfferExpired",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "DriverOfferExpired",
                notification,
                cancellationToken));
    }

    public Task PublishDriverOfferCancelledAsync(
        DriverOfferCancelledEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "DriverOfferCancelled",
                notification,
                cancellationToken),
            SendToDriverAsync(
                notification.DriverId,
                "DriverOfferCancelled",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "DriverOfferCancelled",
                notification,
                cancellationToken));
    }

    public Task PublishDriverMatchedAsync(
        DriverMatchedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return SendToDriverAsync(
            notification.DriverId,
            "BookingMatched",
            notification,
            cancellationToken);
    }

    public Task PublishCustomerConfirmedDriverOfferAsync(
        CustomerConfirmedDriverOfferEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "CustomerConfirmedDriverOffer",
                notification,
                cancellationToken),
            SendToDriverAsync(
                notification.DriverId,
                "CustomerConfirmedDriverOffer",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "CustomerConfirmedDriverOffer",
                notification,
                cancellationToken));
    }

    public Task PublishBookingSearchRadiusExpandedAsync(
        BookingSearchRadiusExpandedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "BookingSearchRadiusExpanded",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "BookingSearchRadiusExpanded",
                notification,
                cancellationToken));
    }

    public Task PublishBookingExpiredAsync(
        BookingExpiredEvent notification,
        CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            SendToUserAsync(
                notification.CustomerId,
                "BookingExpired",
                notification,
                cancellationToken),
            SendToBookingAsync(
                notification.BookingId,
                "BookingExpired",
                notification,
                cancellationToken));
    }

    private Task SendToUserAsync<T>(
        Guid userId,
        string method,
        T payload,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(RealtimeGroups.User(userId))
            .SendAsync(method, payload, cancellationToken);
    }

    private Task SendToDriverAsync<T>(
        Guid driverId,
        string method,
        T payload,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(RealtimeGroups.Driver(driverId))
            .SendAsync(method, payload, cancellationToken);
    }

    private Task SendToBookingAsync<T>(
        long bookingId,
        string method,
        T payload,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(RealtimeGroups.Booking(bookingId))
            .SendAsync(method, payload, cancellationToken);
    }

    private Task SendToTripAsync<T>(
        long tripId,
        string method,
        T payload,
        CancellationToken cancellationToken)
    {
        return _hubContext.Clients
            .Group(RealtimeGroups.Trip(tripId))
            .SendAsync(method, payload, cancellationToken);
    }
}

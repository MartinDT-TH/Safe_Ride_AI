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

        var tasks = new List<Task>
        {
            SendToDriverAsync(
                notification.DriverId,
                "DriverLocationUpdated",
                notification,
                cancellationToken),
            SendToTripAsync(
                notification.TripId.Value,
                "DriverLocationUpdated",
                notification,
                cancellationToken)
        };

        if (notification.CustomerId.HasValue)
        {
            tasks.Add(SendToUserAsync(
                notification.CustomerId.Value,
                "DriverLocationUpdated",
                notification,
                cancellationToken));
        }

        return Task.WhenAll(tasks);
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

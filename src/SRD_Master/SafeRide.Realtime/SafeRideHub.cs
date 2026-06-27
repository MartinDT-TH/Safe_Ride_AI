using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Realtime;

[Authorize]
public sealed class SafeRideHub : Hub
{
    private readonly IDriverRealtimeService _driverRealtimeService;

    public SafeRideHub(IDriverRealtimeService driverRealtimeService)
    {
        _driverRealtimeService = driverRealtimeService;
    }

    public override async Task OnConnectedAsync()
    {
        if (TryGetUserId(out var userId))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RealtimeGroups.User(userId));
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RealtimeGroups.Driver(userId));
        }

        await base.OnConnectedAsync();
    }

    public Task JoinBooking(long bookingId)
    {
        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.Booking(bookingId));
    }

    public Task LeaveBooking(long bookingId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.Booking(bookingId));
    }

    public Task JoinTrip(long tripId)
    {
        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.Trip(tripId));
    }

    public Task LeaveTrip(long tripId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.Trip(tripId));
    }

    // [Authorize(Roles = "Driver")]
    public async Task UpdateDriverLocation(double latitude, double longitude)
    {
        ValidateCoordinate(latitude, longitude);

        if (!TryGetUserId(out var driverId))
        {
            throw new HubException("Cannot resolve authenticated driver id.");
        }

        await _driverRealtimeService.UpdateDriverLocationAsync(
            driverId,
            latitude,
            longitude,
            Context.ConnectionAborted);
    }

    // [Authorize(Roles = "Driver")]
    public async Task SetDriverOnline(double latitude, double longitude)
    {
        ValidateCoordinate(latitude, longitude);

        if (!TryGetUserId(out var driverId))
        {
            throw new HubException("Cannot resolve authenticated driver id.");
        }

        await _driverRealtimeService.SetDriverOnlineAsync(
            driverId,
            latitude,
            longitude,
            Context.ConnectionAborted);
    }

    // [Authorize(Roles = "Driver")]
    public async Task SetDriverOffline()
    {
        if (!TryGetUserId(out var driverId))
        {
            throw new HubException("Cannot resolve authenticated driver id.");
        }

        await _driverRealtimeService.SetDriverOfflineAsync(
            driverId,
            Context.ConnectionAborted);
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }

    private static void ValidateCoordinate(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude)
            || !double.IsFinite(longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            throw new HubException("Driver location coordinates are invalid.");
        }
    }
}

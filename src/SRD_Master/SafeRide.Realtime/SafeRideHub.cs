using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Auth;
using SafeRide.Contracts.Requests.Drivers;

namespace SafeRide.Realtime;

[Authorize]
public sealed class SafeRideHub : Hub
{
    private readonly IDriverRealtimeService _driverRealtimeService;
    private readonly ITripContinuationAccessService _tripContinuationAccessService;
    private readonly ITripSharingService _tripSharingService;

    public SafeRideHub(
        IDriverRealtimeService driverRealtimeService,
        ITripContinuationAccessService tripContinuationAccessService,
        ITripSharingService tripSharingService)
    {
        _driverRealtimeService = driverRealtimeService;
        _tripContinuationAccessService = tripContinuationAccessService;
        _tripSharingService = tripSharingService;
    }

    public override async Task OnConnectedAsync()
    {
        if (TryGetUserId(out var userId))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RealtimeGroups.User(userId));
            if (Context.User?.IsInRole("Driver") == true)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    RealtimeGroups.Driver(userId));
            }
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    RealtimeGroups.AdminReports);
            }
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    RealtimeGroups.AdminSOS);
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinBooking(long bookingId)
    {
        await EnsureContinuationAllowedAsync(
            TripContinuationOperation.SignalRJoinBooking,
            bookingId: bookingId);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.Booking(bookingId));
    }

    public Task LeaveBooking(long bookingId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.Booking(bookingId));
    }

    public async Task JoinTrip(long tripId)
    {
        await EnsureContinuationAllowedAsync(
            TripContinuationOperation.SignalRJoinTrip,
            tripId: tripId);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.Trip(tripId));
    }

    public Task LeaveTrip(long tripId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.Trip(tripId));
    }

    [Authorize(Roles = "Admin")]
    public Task JoinAdminReportsGroup()
    {
        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.AdminReports);
    }

    [Authorize(Roles = "Admin")]
    public Task LeaveAdminReportsGroup()
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.AdminReports);
    }

    public async Task SubscribeSharedTrip(long tripShareId)
    {
        if (!TryGetUserId(out var userId)
            || !await _tripSharingService.CanSubscribeAsync(
                tripShareId,
                userId,
                Context.ConnectionAborted))
        {
            throw new HubException("Bạn không có quyền theo dõi chuyến đi được chia sẻ này.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.TripShare(tripShareId));
    }

    public Task UnsubscribeSharedTrip(long tripShareId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.TripShare(tripShareId));
    }

    [Authorize(Roles = "Admin")]
    public Task JoinAdminSOSGroup()
    {
        return Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.AdminSOS);
    }

    [Authorize(Roles = "Admin")]
    public Task LeaveAdminSOSGroup()
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.AdminSOS);
    }

    public Task SendInAppCallOffer(InAppCallSignal signal)
    {
        ValidateInAppCallSignal(signal);

        return Clients
            .OthersInGroup(RealtimeGroups.Trip(signal.TripId))
            .SendAsync("InAppCallOffer", signal, Context.ConnectionAborted);
    }

    public Task SendInAppCallAnswer(InAppCallSignal signal)
    {
        ValidateInAppCallSignal(signal);

        return Clients
            .OthersInGroup(RealtimeGroups.Trip(signal.TripId))
            .SendAsync("InAppCallAnswer", signal, Context.ConnectionAborted);
    }

    public Task SendInAppCallIceCandidate(InAppCallSignal signal)
    {
        ValidateInAppCallSignal(signal);

        return Clients
            .OthersInGroup(RealtimeGroups.Trip(signal.TripId))
            .SendAsync("InAppCallIceCandidate", signal, Context.ConnectionAborted);
    }

    public Task RejectInAppCall(InAppCallSignal signal)
    {
        ValidateInAppCallSignal(signal);

        return Clients
            .OthersInGroup(RealtimeGroups.Trip(signal.TripId))
            .SendAsync("InAppCallRejected", signal, Context.ConnectionAborted);
    }

    public Task EndInAppCall(InAppCallSignal signal)
    {
        ValidateInAppCallSignal(signal);

        return Clients
            .OthersInGroup(RealtimeGroups.Trip(signal.TripId))
            .SendAsync("InAppCallEnded", signal, Context.ConnectionAborted);
    }

    [Authorize(Roles = "Driver")]
    public async Task UpdateDriverLocation(double latitude, double longitude)
    {
        ValidateCoordinate(latitude, longitude);

        if (!TryGetUserId(out var driverId))
        {
            throw new HubException("Cannot resolve authenticated driver id.");
        }

        await EnsureContinuationAllowedAsync(
            TripContinuationOperation.DriverLocation);

        await _driverRealtimeService.UpdateDriverLocationAsync(
            driverId,
            latitude,
            longitude,
            Context.ConnectionAborted);
    }

    [Authorize(Roles = "Driver")]
    public async Task UpdateDriverLocationDetailed(UpdateDriverLocationRequest request)
    {
        ValidateCoordinate(request.Latitude, request.Longitude);

        if (!TryGetUserId(out var driverId))
        {
            throw new HubException("Cannot resolve authenticated driver id.");
        }

        await EnsureContinuationAllowedAsync(
            TripContinuationOperation.DriverLocation);

        await _driverRealtimeService.UpdateDriverLocationAsync(
            driverId,
            new DriverLocationUpdateInput(
                request.Latitude,
                request.Longitude,
                request.ClientTimestampUtc,
                request.Sequence,
                request.AccuracyMeters,
                request.SpeedMetersPerSecond),
            Context.ConnectionAborted);
    }

    [Authorize(Roles = "Driver")]
    public async Task SetDriverOnline(double latitude, double longitude)
    {
        ValidateCoordinate(latitude, longitude);

        if (!TryGetUserId(out var driverId))
        {
            throw new HubException("Cannot resolve authenticated driver id.");
        }

        EnsureNormalSession();

        await _driverRealtimeService.SetDriverOnlineAsync(
            driverId,
            latitude,
            longitude,
            Context.ConnectionAborted);
    }

    [Authorize(Roles = "Driver")]
    public async Task SetDriverOffline()
    {
        if (!TryGetUserId(out var driverId))
        {
            throw new HubException("Cannot resolve authenticated driver id.");
        }

        EnsureNormalSession();

        await _driverRealtimeService.SetDriverOfflineAsync(
            driverId,
            Context.ConnectionAborted);
    }

    private async Task EnsureContinuationAllowedAsync(
        TripContinuationOperation operation,
        long? tripId = null,
        long? bookingId = null)
    {
        if (Context.User is null)
        {
            throw new HubException("Cannot resolve authenticated user.");
        }

        var allowed = await _tripContinuationAccessService.IsAllowedAsync(
            Context.User,
            operation,
            tripId,
            bookingId,
            Context.ConnectionAborted);
        if (!allowed)
        {
            throw new HubException("Trip continuation session cannot access this resource.");
        }
    }

    private void EnsureNormalSession()
    {
        if (string.Equals(
                Context.User?.FindFirstValue(AuthClaimTypes.SessionMode),
                AuthSessionModes.TripContinuation,
                StringComparison.Ordinal))
        {
            throw new HubException("Trip continuation session cannot change driver presence.");
        }
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

    private static void ValidateInAppCallSignal(InAppCallSignal signal)
    {
        if (signal.TripId <= 0)
        {
            throw new HubException("Trip id is required for in-app call signaling.");
        }

        if (string.IsNullOrWhiteSpace(signal.CallId))
        {
            throw new HubException("Call id is required for in-app call signaling.");
        }
    }
}

public sealed record InAppCallSignal(
    long TripId,
    long? BookingId,
    string CallId,
    string? Sdp,
    string? SdpType,
    string? Candidate,
    string? SdpMid,
    int? SdpMLineIndex);

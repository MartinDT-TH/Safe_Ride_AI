using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;

namespace SafeRide.Realtime;

[Authorize]
public sealed class TripChatHub : Hub
{
    private readonly ITripChatService _tripChatService;

    public TripChatHub(ITripChatService tripChatService)
    {
        _tripChatService = tripChatService;
    }

    public async Task JoinTripChat(long tripId)
    {
        var userId = GetUserId();
        await ExecuteChatOperationAsync(
            () => _tripChatService.EnsureCanAccessTripChatAsync(
                userId,
                tripId,
                Context.ConnectionAborted));

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.TripChat(tripId),
            Context.ConnectionAborted);
    }

    public Task LeaveTripChat(long tripId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.TripChat(tripId),
            Context.ConnectionAborted);
    }

    public async Task SendTripMessage(long tripId, string message)
    {
        var userId = GetUserId();
        var payload = await ExecuteChatOperationAsync(
            () => _tripChatService.SendMessageAsync(
                userId,
                tripId,
                message,
                Context.ConnectionAborted));

        await Clients
            .Group(RealtimeGroups.TripChat(tripId))
            .SendAsync(
                "TripMessageReceived",
                payload,
                Context.ConnectionAborted);
    }

    private Guid GetUserId()
    {
        if (!Guid.TryParse(
                Context.User?.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            throw new HubException("Cannot resolve authenticated user id.");
        }

        return userId;
    }

    private static async Task ExecuteChatOperationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (BookingException exception)
        {
            throw new HubException(exception.Message);
        }
    }

    private static async Task<T> ExecuteChatOperationAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (BookingException exception)
        {
            throw new HubException(exception.Message);
        }
    }
}

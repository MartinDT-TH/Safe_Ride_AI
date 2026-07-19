using SafeRide.Application.Features.Trips.DTOs;

namespace SafeRide.Application.Common.Interfaces;

public interface ITripChatService
{
    Task EnsureCanAccessTripChatAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken = default);

    Task<TripChatMessageDto> SendMessageAsync(
        Guid senderUserId,
        long tripId,
        string message,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripChatMessageDto>> GetMessagesAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken = default);

    Task ShortenMessageTtlAsync(
        long tripId,
        CancellationToken cancellationToken = default);
}

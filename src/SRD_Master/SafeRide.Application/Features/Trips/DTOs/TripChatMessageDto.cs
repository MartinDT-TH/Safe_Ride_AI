namespace SafeRide.Application.Features.Trips.DTOs;

public sealed record TripChatMessageDto(
    Guid Id,
    long TripId,
    Guid SenderUserId,
    string SenderName,
    string MessageType,
    string Message,
    string? ImageUrl,
    DateTime SentAt);

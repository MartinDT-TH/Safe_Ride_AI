namespace SafeRide.Application.Features.Trips.DTOs;

public sealed record TripChatUnreadSummaryDto(
    long TotalUnread,
    IReadOnlyList<TripChatUnreadItemDto> Items);

public sealed record TripChatUnreadItemDto(
    long TripId,
    long BookingId,
    long UnreadCount,
    string LastMessagePreview,
    DateTime LastMessageAt);

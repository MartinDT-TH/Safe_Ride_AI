namespace SafeRide.Application.Features.Trips.DTOs;

public sealed record TripChatMessageDto(
    Guid Id,
    long TripId,
    Guid SenderUserId,
    string SenderName,
    string MessageType,
    string Message,
    string? ImageUrl,
    DateTime SentAt)
{
    public long BookingId { get; init; }

    public Guid SenderId => SenderUserId;

    public string MessagePreview => MessageType == "Image"
        ? "Hình ảnh"
        : Message;

    public DateTime CreatedAt => SentAt;
}

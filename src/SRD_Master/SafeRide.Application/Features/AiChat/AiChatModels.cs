namespace SafeRide.Application.Features.AiChat;

public sealed record SendAiChatMessageRequest(
    string Message,
    string? ConversationId,
    AiCurrentLocationRequest? CurrentLocation = null,
    string? LanguageCode = null);

public sealed record AiCurrentLocationRequest(
    string? Address,
    double Latitude,
    double Longitude);

public sealed record AiBookingLocationDto(string Address, double Latitude, double Longitude);

public sealed record AiBookingDraftDto(
    AiBookingLocationDto Pickup,
    AiBookingLocationDto Destination,
    string? VehicleQuery = null,
    string? PromotionCode = null);

public sealed record AiChatMessageDto(
    string Id,
    string Role,
    string Content,
    DateTime CreatedAt,
    AiBookingDraftDto? BookingDraft = null,
    bool IsAudio = false,
    string? AudioUrl = null,
    string? AudioMimeType = null,
    long? AudioSizeBytes = null);

public sealed record AiChatReplyDto(
    string ConversationId,
    AiChatMessageDto UserMessage,
    AiChatMessageDto AssistantMessage,
    AiBookingDraftDto? BookingDraft);

public sealed record AiConversationDto(
    string Id,
    string Title,
    DateTime UpdatedAt);

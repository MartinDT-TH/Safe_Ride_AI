using SafeRide.Application.Features.AiChat;

namespace SafeRide.Application.Common.Interfaces;

public interface IAiChatService
{
    Task<AiChatReplyDto> SendAsync(
        Guid userId,
        SendAiChatMessageRequest request,
        CancellationToken cancellationToken);

    Task<AiChatReplyDto> SendAudioAsync(
        Guid userId,
        Stream audio,
        string mimeType,
        string? conversationId,
        AiCurrentLocationRequest? currentLocation,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiConversationDto>> GetConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiChatMessageDto>> GetMessagesAsync(
        Guid userId,
        string conversationId,
        CancellationToken cancellationToken);

    Task DeleteConversationAsync(
        Guid userId,
        string conversationId,
        CancellationToken cancellationToken);
}

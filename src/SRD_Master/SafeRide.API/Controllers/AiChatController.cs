using System.Security.Claims;
using System.Net;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AiChat;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/ai-chat")]
public sealed class AiChatController(IAiChatService chatService) : ControllerBase
{
    [HttpPost("messages")]
    [ProducesResponseType<AiChatReplyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send(
        [FromBody] SendAiChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return UnauthorizedProblem();
        if (string.IsNullOrWhiteSpace(request.Message))
            return ProblemResponse(400, "ai_chat.invalid_message", "Vui lòng nhập nội dung tin nhắn.");

        try
        {
            return Ok(await chatService.SendAsync(userId, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return ProblemResponse(400, "ai_chat.invalid_message", exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            return ProblemResponse(404, "ai_chat.not_found", exception.Message);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode is HttpStatusCode.TooManyRequests or
                HttpStatusCode.ServiceUnavailable)
        {
            return ProblemResponse(
                StatusCodes.Status503ServiceUnavailable,
                "ai_chat.provider_rate_limited",
                "Trợ lý AI đang tạm quá tải. Vui lòng thử lại sau.");
        }
    }

    [HttpPost("audio")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType<AiChatReplyDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SendAudio(
        IFormFile audio,
        [FromForm] string? conversationId,
        [FromForm] string? currentAddress,
        [FromForm] string? currentLatitude,
        [FromForm] string? currentLongitude,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return UnauthorizedProblem();
        if (audio.Length == 0)
            return ProblemResponse(400, "ai_chat.invalid_audio", "Vui lòng gửi file ghi âm.");

        var hasLatitude = double.TryParse(
            currentLatitude,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var latitude);
        var hasLongitude = double.TryParse(
            currentLongitude,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var longitude);
        if (!hasLatitude || !hasLongitude ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return ProblemResponse(
                400,
                "ai_chat.location_required",
                "Không lấy được GPS hiện tại. Vui lòng bật vị trí rồi gửi lại tin nhắn thoại.");
        }
        var location = hasLatitude && hasLongitude
            ? new AiCurrentLocationRequest(
                currentAddress,
                latitude,
                longitude)
            : null;
        try
        {
            await using var stream = audio.OpenReadStream();
            return Ok(await chatService.SendAudioAsync(
                userId,
                stream,
                audio.ContentType,
                conversationId,
                location,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return ProblemResponse(400, "ai_chat.invalid_audio", exception.Message);
        }
        catch (HttpRequestException exception)
            when (exception.StatusCode is HttpStatusCode.TooManyRequests or
                HttpStatusCode.ServiceUnavailable)
        {
            return ProblemResponse(
                StatusCodes.Status503ServiceUnavailable,
                "ai_chat.provider_unavailable",
                "Trợ lý AI đang tạm quá tải. File ghi âm chưa được gửi, vui lòng thử lại sau.");
        }
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return UnauthorizedProblem();
        return Ok(await chatService.GetConversationsAsync(userId, cancellationToken));
    }

    [HttpGet("conversations/{conversationId}/messages")]
    public async Task<IActionResult> GetMessages(
        string conversationId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return UnauthorizedProblem();
        try
        {
            return Ok(await chatService.GetMessagesAsync(userId, conversationId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return ProblemResponse(404, "ai_chat.not_found", exception.Message);
        }
    }

    [HttpDelete("conversations/{conversationId}")]
    public async Task<IActionResult> Delete(
        string conversationId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return UnauthorizedProblem();
        try
        {
            await chatService.DeleteConversationAsync(userId, conversationId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return ProblemResponse(404, "ai_chat.not_found", exception.Message);
        }
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private ObjectResult UnauthorizedProblem() =>
        ProblemResponse(401, "auth.unauthorized", "Phiên đăng nhập không hợp lệ.");

    private ObjectResult ProblemResponse(int status, string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = "Không thể xử lý yêu cầu",
            Detail = detail,
            Type = $"https://saferide.vn/problems/{code}"
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return StatusCode(status, problem);
    }
}

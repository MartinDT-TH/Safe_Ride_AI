using System.Security.Claims;
using System.Net;
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
            when (exception.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return ProblemResponse(
                StatusCodes.Status503ServiceUnavailable,
                "ai_chat.provider_rate_limited",
                "Trợ lý AI đang tạm quá tải. Vui lòng thử lại sau.");
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

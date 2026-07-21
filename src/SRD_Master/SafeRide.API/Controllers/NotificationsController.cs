using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Notifications.Commands.MarkNotificationAsRead;
using SafeRide.Application.Features.Notifications.Queries.GetUserNotifications;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public NotificationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new GetUserNotificationsQuery(userId, page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{notificationId:long}/read")]
    public async Task<IActionResult> MarkAsRead(
        long notificationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new MarkNotificationAsReadCommand(userId, notificationId),
            cancellationToken);
        return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}

using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.AdminNotifications.Commands.ApproveAdminNotification;
using SafeRide.Application.Features.AdminNotifications.Commands.CreateAdminNotification;
using SafeRide.Application.Features.AdminNotifications.Commands.RejectAdminNotification;
using SafeRide.Application.Features.AdminNotifications.Queries.GetAdminNotifications;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/notifications")]
public sealed class AdminNotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminNotificationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? audience = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetAdminNotificationsQuery(
                page,
                pageSize,
                search,
                status,
                type,
                audience),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification(
        [FromBody] CreateAdminNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new CreateAdminNotificationCommand(
                adminUserId,
                request.Title,
                request.Content,
                request.NotificationType,
                request.TargetAudience),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{notificationId:long}/approve")]
    public async Task<IActionResult> ApproveNotification(
        long notificationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new ApproveAdminNotificationCommand(notificationId, adminUserId),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{notificationId:long}/reject")]
    public async Task<IActionResult> RejectNotification(
        long notificationId,
        [FromBody] RejectAdminNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new RejectAdminNotificationCommand(
                notificationId,
                adminUserId,
                request.RejectionReason),
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

public sealed record CreateAdminNotificationRequest(
    NotificationAudience TargetAudience,
    string NotificationType,
    string Title,
    string Content);

public sealed record RejectAdminNotificationRequest(string RejectionReason);

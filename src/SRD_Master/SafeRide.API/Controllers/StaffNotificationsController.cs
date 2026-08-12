using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.StaffNotifications;
using SafeRide.Application.Features.StaffNotifications.Commands.CreateStaffNotificationRequest;
using SafeRide.Application.Features.StaffNotifications.Queries.GetStaffNotificationRequests;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/staff/notifications")]
public sealed class StaffNotificationsController : ControllerBase
{
    private readonly ISender _sender;

    public StaffNotificationsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType<StaffNotificationRequestPagedResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffNotificationRequestPagedResult>> GetNotificationRequests(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? audience = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var staffUserId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new GetStaffNotificationRequestsQuery(
                staffUserId,
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
    [ProducesResponseType<StaffNotificationRequestResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffNotificationRequestResponse>> CreateNotificationRequest(
        [FromBody] CreateStaffNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var staffUserId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new CreateStaffNotificationRequestCommand(
                staffUserId,
                request.Title,
                request.Content,
                request.NotificationType,
                request.TargetAudience),
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

public sealed record CreateStaffNotificationRequest(
    NotificationAudience TargetAudience,
    string NotificationType,
    string Title,
    string Content);

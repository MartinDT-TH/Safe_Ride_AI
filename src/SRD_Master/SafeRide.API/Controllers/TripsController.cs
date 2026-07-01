using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Ratings.Commands.SubmitTripRating;
using SafeRide.Contracts.Requests.Trips;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/trips")]
public sealed class TripsController : ControllerBase
{
    private readonly ITripStatusService _tripStatusService;
    private readonly ISender _sender;

    public TripsController(
        ITripStatusService tripStatusService,
        ISender sender)
    {
        _tripStatusService = tripStatusService;
        _sender = sender;
    }

    [HttpPatch("{tripId:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        long tripId,
        [FromBody] UpdateTripStatusRequest request,
        CancellationToken cancellationToken)
    {
        // Flow: driver status updates go through the trip state machine in TripStatusService.
        if (!TryGetDriverId(out var driverId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Cannot resolve authenticated driver account."
            });
        }

        await _tripStatusService.UpdateDriverTripStatusAsync(
            driverId,
            tripId,
            request.TripStatus,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{tripId:long}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(
        long tripId,
        CancellationToken cancellationToken)
    {
        // Flow: completing a trip is terminal and settles booking status, promotion usage, and driver availability.
        if (!TryGetDriverId(out var userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Cannot resolve authenticated account."
            });
        }

        await _tripStatusService.CompleteTripAsync(
            userId,
            tripId,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{tripId:long}/rating")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType<SubmitTripRatingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitTripRatingResponse>> SubmitRating(
        long tripId,
        [FromBody] SubmitTripRatingRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var customerId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Không xác định được tài khoản khách hàng."
            });
        }

        var response = await _sender.Send(
            new SubmitTripRatingCommand(
                tripId,
                customerId,
                request.RatingScore,
                request.Comment),
            cancellationToken);

        return Ok(response);
    }

    private bool TryGetDriverId(out Guid driverId)
    {
        return TryGetUserId(out driverId);
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}

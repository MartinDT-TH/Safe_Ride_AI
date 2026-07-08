using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.API.Authorization;
using SafeRide.Application.Features.Auth;
using SafeRide.Application.Features.Ratings.Commands.SubmitTripRating;
using SafeRide.Contracts.Requests.Trips;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/feedbacks")]
public sealed class FeedbacksController : ControllerBase
{
    private readonly ISender _sender;

    public FeedbacksController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("trips/{tripId:long}/rating")]
    [Authorize(Roles = "Customer")]
    [AllowTripContinuation(TripContinuationOperation.TripRating)]
    [ProducesResponseType<SubmitTripRatingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitTripRatingResponse>> SubmitTripRating(
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

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}

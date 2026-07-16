using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Ratings.Commands.SubmitTripRating;
using SafeRide.Application.Features.Reports.Commands.SubmitBookingReport;
using SafeRide.Application.Features.Reports.Commands.SubmitTripReport;
using SafeRide.Contracts.Requests.Feedbacks;
using SafeRide.Contracts.Requests.Trips;
using SafeRide.Contracts.Responses.Feedbacks;

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

    [HttpPost("bookings/{bookingId:long}/reports")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType<SubmitTripReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitTripReportResponse>> SubmitBookingReport(
        long bookingId,
        [FromBody] SubmitTripReportRequest request,
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
            new SubmitBookingReportCommand(
                bookingId,
                customerId,
                request.Subject,
                request.Description),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("trips/{tripId:long}/reports")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType<SubmitTripReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmitTripReportResponse>> SubmitTripReport(
        long tripId,
        [FromBody] SubmitTripReportRequest request,
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
            new SubmitTripReportCommand(
                tripId,
                customerId,
                request.Subject,
                request.Description),
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

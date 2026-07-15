using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.API.Authorization;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Auth;
using SafeRide.Application.Features.Ratings.Commands.SubmitTripRating;
using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Application.Features.TripSharing;
using SafeRide.Contracts.Requests.Trips;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/trips")]
public sealed class TripsController : ControllerBase
{
    private readonly ITripStatusService _tripStatusService;
    private readonly ISender _sender;
    private readonly ITripSharingService _tripSharingService;

    public TripsController(
        ITripStatusService tripStatusService,
        ISender sender,
        ITripSharingService tripSharingService)
    {
        _tripStatusService = tripStatusService;
        _sender = sender;
        _tripSharingService = tripSharingService;
    }

    [HttpPost("{tripId:long}/shares")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType<CreateTripShareResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateTripShareResult>> CreateShare(
        long tripId,
        [FromBody] CreateTripShareRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var customerId))
        {
            return Unauthorized();
        }

        return Ok(await _tripSharingService.CreateAsync(
            tripId,
            customerId,
            request.RecipientPhoneNumber,
            cancellationToken));
    }

    [HttpGet("{tripId:long}/shares")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType<IReadOnlyList<TripShareListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TripShareListItemDto>>> ListShares(
        long tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var customerId))
        {
            return Unauthorized();
        }

        return Ok(await _tripSharingService.ListAsync(
            tripId,
            customerId,
            cancellationToken));
    }

    [HttpDelete("{tripId:long}/shares/{tripShareId:long}")]
    [Authorize(Roles = "Customer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeShare(
        long tripId,
        long tripShareId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var customerId))
        {
            return Unauthorized();
        }

        await _tripSharingService.RevokeAsync(
            tripId,
            tripShareId,
            customerId,
            cancellationToken);
        return NoContent();
    }

    [HttpPatch("{tripId:long}/status")]
    [AllowTripContinuation(TripContinuationOperation.TripStatusUpdate)]
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

    [HttpPost("{tripId:long}/end")]
    [AllowTripContinuation(TripContinuationOperation.TripStatusUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> End(
        long tripId,
        CancellationToken cancellationToken)
    {
        if (!TryGetDriverId(out var driverId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Cannot resolve authenticated driver account."
            });
        }

        await _tripStatusService.EndTripAsync(
            driverId,
            tripId,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{tripId:long}/return-confirmation/customer")]
    [Authorize(Roles = "Customer")]
    [AllowTripContinuation(TripContinuationOperation.TripReturnConfirmation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmReturnByCustomer(
        long tripId,
        [FromBody] ConfirmTripReturnRequest request,
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

        await _tripStatusService.ConfirmReturnByCustomerAsync(
            customerId,
            tripId,
            request.VehicleReturnedConfirmed,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Driver confirms vehicle return on behalf of the customer.
    /// Requires 1–3 evidence photos (multipart/form-data, field name: evidence).
    /// Server captures GPS from Redis; the driver cannot supply source-of-truth location.
    /// Moves trip WAITING_RETURN_CONFIRM to RETURN_CONFIRMED, then WAITING_PAYMENT.
    /// </summary>
    [HttpPost("{tripId:long}/return-confirmation/driver")]
    [Authorize(Roles = "Driver")]
    [AllowTripContinuation(TripContinuationOperation.TripReturnConfirmation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ConfirmReturnByDriver(
        long tripId,
        [FromForm] string? note,
        CancellationToken cancellationToken)
    {
        if (!TryGetDriverId(out var driverId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Không xác định được tài khoản tài xế."
            });
        }

        var files = Request.Form.Files;
        if (files.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Thiếu ảnh bằng chứng",
                Detail = "Cần tải lên ít nhất 1 ảnh bằng chứng bàn giao xe."
            });
        }

        if (files.Count > 3)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Quá nhiều ảnh",
                Detail = "Không được tải lên quá 3 ảnh bằng chứng."
            });
        }

        // Convert IFormFile to application-layer DTO to keep ASP.NET Core types
        // out of the Application/Infrastructure layers.
        var evidenceItems = files
            .Select(f => new ReturnEvidenceItem(
                f.OpenReadStream(),
                f.FileName ?? "evidence",
                f.ContentType ?? "image/jpeg",
                f.Length))
            .ToList();

        await _tripStatusService.ConfirmReturnByDriverAsync(
            driverId,
            tripId,
            evidenceItems,
            note,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{tripId:long}/complete")]
    [AllowTripContinuation(TripContinuationOperation.TripStatusUpdate)]
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
    [AllowTripContinuation(TripContinuationOperation.TripRating)]
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

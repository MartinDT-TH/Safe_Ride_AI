using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Contracts.Requests.Trips;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/trips")]
public sealed class TripsController : ControllerBase
{
    private readonly ITripStatusService _tripStatusService;
    private readonly ApplicationDbContext _dbContext;

    public TripsController(
        ITripStatusService tripStatusService,
        ApplicationDbContext dbContext)
    {
        _tripStatusService = tripStatusService;
        _dbContext = dbContext;
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitRating(
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
                Detail = "Cannot resolve authenticated customer account."
            });
        }

        if (request.RatingScore is < 1 or > 5)
        {
            throw new BookingException(
                "rating.invalid_score",
                "Rating score must be between 1 and 5.",
                StatusCodes.Status400BadRequest);
        }

        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
            .Include(x => x.Rating)
            .FirstOrDefaultAsync(
                x => x.Id == tripId && x.Booking.CustomerId == customerId,
                cancellationToken);

        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Trip not found.",
                StatusCodes.Status404NotFound);
        }

        if (trip.TripStatus != TripStatus.COMPLETED)
        {
            throw new BookingException(
                "rating.trip_not_completed",
                "Only completed trips can be rated.",
                StatusCodes.Status409Conflict);
        }

        if (trip.Rating is not null)
        {
            throw new BookingException(
                "rating.already_submitted",
                "This trip has already been rated.",
                StatusCodes.Status409Conflict);
        }

        var comment = string.IsNullOrWhiteSpace(request.Comment)
            ? null
            : request.Comment.Trim();

        _dbContext.Ratings.Add(new Rating
        {
            TripId = trip.Id,
            CustomerId = customerId,
            DriverId = trip.DriverId,
            RatingScore = request.RatingScore,
            Comment = comment,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
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

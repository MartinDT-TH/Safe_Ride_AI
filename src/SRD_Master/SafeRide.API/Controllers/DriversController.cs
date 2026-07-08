using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using SafeRide.API.Authorization;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Auth;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Application.Features.Drivers.Commands.SetDriverOffline;
using SafeRide.Application.Features.Drivers.Queries.GetActiveDriverTrip;
using SafeRide.Application.Features.Drivers.Queries.GetNearbyDrivers;
using SafeRide.Contracts.Requests.Drivers;
using SafeRide.Contracts.Responses.Bookings;
using SafeRide.Contracts.Responses.Drivers;
using SafeRide.Domain.Enums;
using System.Security.Claims;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/drivers")]
public sealed class DriversController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IBookingAssignmentService _bookingAssignmentService;
    private readonly IDriverRealtimeService _driverRealtimeService;

    public DriversController(
        ISender sender,
        IBookingAssignmentService bookingAssignmentService,
        IDriverRealtimeService driverRealtimeService)
    {
        _sender = sender;
        _bookingAssignmentService = bookingAssignmentService;
        _driverRealtimeService = driverRealtimeService;
    }

    [HttpGet("nearby")]
    [ProducesResponseType<List<NearbyDriverResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NearbyDriverResponse>>> GetNearbyDrivers(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm = 5.0,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var results = await _sender.Send(
            new GetNearbyDriversQuery(latitude, longitude, radiusKm, limit),
            cancellationToken);

        return Ok(results);
    }

    [Authorize(Roles = "Driver")]
    [HttpGet("trips/active")]
    [AllowTripContinuation(TripContinuationOperation.ActiveTripRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetActiveTrip(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var activeTrip = await _sender.Send(
            new GetActiveDriverTripQuery(driverId),
            cancellationToken);

        return activeTrip is null ? NoContent() : Ok(activeTrip);
    }

    [Authorize(Roles = "Driver")]
    [HttpPost("online")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetOnline(
        [FromBody] UpdateDriverLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        await _driverRealtimeService.SetDriverOnlineAsync(
            driverId,
            request.Latitude,
            request.Longitude,
            cancellationToken);

        return NoContent();
    }

    [Authorize(Roles = "Driver")]
    [HttpPost("offline")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetOffline(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var result = await _sender.Send(
            new SetDriverOfflineCommand(driverId),
            cancellationToken);

        if (!result.CanSetOffline)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Cannot set offline",
                Detail = "You cannot go offline while busy or having an active trip."
            });
        }

        return NoContent();
    }

    [Authorize(Roles = "Driver")]
    [HttpPatch("location")]
    [AllowTripContinuation(TripContinuationOperation.DriverLocation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateLocation(
        [FromBody] UpdateDriverLocationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        await _driverRealtimeService.UpdateDriverLocationAsync(
            driverId,
            new DriverLocationUpdateInput(
                request.Latitude,
                request.Longitude,
                request.ClientTimestampUtc,
                request.Sequence,
                request.AccuracyMeters,
                request.SpeedMetersPerSecond),
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Driver accepts the offer. This sets the offer status to DriverAccepted but does NOT create a Trip yet.
    /// The Trip is created only when the customer confirms the accepted driver.
    /// </summary>
    [Authorize(Roles = "Driver")]
    [HttpPost("offers/{offerId:long}/accept")]
    [HttpPost("/api/driver-offers/{offerId:long}/accept")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> AcceptOffer(
        long offerId,
        CancellationToken cancellationToken)
    {
        // Flow: driver acceptance only moves the offer to DriverAccepted; customer confirmation creates the Trip.
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var result = await _bookingAssignmentService.AcceptDriverOfferAsync(
            driverId,
            offerId,
            cancellationToken);

        return Ok(ToResponse(result));
    }

    [Authorize(Roles = "Driver")]
    [HttpPost("offers/{offerId:long}/reject")]
    [HttpPost("/api/driver-offers/{offerId:long}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectOffer(
        long offerId,
        CancellationToken cancellationToken)
    {
        // Flow: driver rejection closes this offer and lets matching search for another candidate.
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        await _bookingAssignmentService.RejectDriverOfferAsync(
            driverId,
            offerId,
            cancellationToken);

        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }

    private static BookingResponse ToResponse(CreateBookingResponse result)
    {
        return new BookingResponse(
            result.BookingId,
            result.BookingType,
            result.BookingStatus,
            result.ScheduledAt,
            result.EstimatedDistanceKm,
            result.EstimatedDurationMinutes,
            result.EstimatedFare,
            result.EncodedPolyline,
            result.Message,
            ToResponse(result.DriverOffer),
            TripStatus: result.TripId.HasValue ? TripStatus.ACCEPTED : null,
            TripId: result.TripId,
            OriginalFare: result.OriginalFare,
            PromotionCode: result.PromotionCode,
            DiscountAmount: result.DiscountAmount,
            FinalFare: result.FinalFare,
            CurrentSearchRadiusKm: result.CurrentSearchRadiusKm,
            ExpiresAt: result.ExpiresAt,
            EstimatedRemainingSeconds: result.EstimatedRemainingSeconds,
            MatchingMessage: result.MatchingMessage);
    }

    private static BookingDriverOfferResponse? ToResponse(
        BookingDriverOfferDto? driverOffer)
    {
        return driverOffer is null
            ? null
            : new BookingDriverOfferResponse(
                driverOffer.OfferId,
                driverOffer.DriverId,
                driverOffer.DriverName,
                driverOffer.DriverAvatarUrl,
                driverOffer.Rating,
                driverOffer.TripCount,
                driverOffer.ExperienceYears,
                driverOffer.LicenseClass,
                driverOffer.ExpiresAt,
                driverOffer.OfferStatus,
                driverOffer.DriverLatitude,
                driverOffer.DriverLongitude,
                driverOffer.CustomerConfirmRemainingSeconds);
    }
}

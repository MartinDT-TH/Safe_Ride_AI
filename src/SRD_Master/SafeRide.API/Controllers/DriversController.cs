using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Contracts.Responses.Bookings;
using SafeRide.Contracts.Responses.Drivers;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Security.Claims;
using System.Text.Json;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/drivers")]
public sealed class DriversController : ControllerBase
{
    private readonly IRedisService _redisService;
    private readonly IBookingAssignmentService _bookingAssignmentService;
    private readonly ApplicationDbContext _dbContext;

    public DriversController(
        IRedisService redisService,
        IBookingAssignmentService bookingAssignmentService,
        ApplicationDbContext dbContext)
    {
        _redisService = redisService;
        _bookingAssignmentService = bookingAssignmentService;
        _dbContext = dbContext;
    }

    [HttpGet("nearby")]
    [ProducesResponseType<List<NearbyDriverResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NearbyDriverResponse>>> GetNearbyDrivers(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm = 5.0,
        [FromQuery] int limit = 10)
    {
        var driverIds = await _redisService.GeoRadiusAsync(
            RedisKeys.OnlineDriversGeo,
            longitude,
            latitude,
            radiusKm,
            limit);

        var tasks = driverIds.Select(async id =>
        {
            var guid = Guid.Parse(id);
            var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(guid));
            if (string.IsNullOrEmpty(locationJson)) return null;

            var cache = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
            return cache is null ? null : new NearbyDriverResponse(
                guid,
                cache.Latitude,
                cache.Longitude);
        });

        var results = await Task.WhenAll(tasks);
        return Ok(results.Where(x => x is not null).ToList());
    }

    [Authorize(Roles = "Driver")]
    [HttpGet("trips/active")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetActiveTrip(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var activeTrip = await _dbContext.Trips
            .AsNoTracking()
            .Where(trip => trip.DriverId == driverId
                && (trip.TripStatus == TripStatus.ACCEPTED
                    || trip.TripStatus == TripStatus.DRIVER_ARRIVING
                    || trip.TripStatus == TripStatus.ARRIVED
                    || trip.TripStatus == TripStatus.IN_PROGRESS))
            .OrderByDescending(trip => trip.DriverAssignedAt ?? trip.CreatedAt)
            .Select(trip => new
            {
                bookingId = trip.BookingId,
                tripId = trip.Id,
                tripStatus = trip.TripStatus
            })
            .FirstOrDefaultAsync(cancellationToken);

        return activeTrip is null ? NoContent() : Ok(activeTrip);
    }

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
                driverOffer.CustomerConfirmRemainingSeconds);
    }
}

public record DriverLocationCache(
    Guid DriverId,
    double Latitude,
    double Longitude,
    DateTime UpdatedAt);

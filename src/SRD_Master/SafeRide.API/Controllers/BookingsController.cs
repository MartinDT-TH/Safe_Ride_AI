using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Bookings.Commands.CancelBooking;
using SafeRide.Application.Features.Bookings.Commands.ConfirmDriver;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Application.Features.Bookings.Commands.RejectDriver;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Application.Features.Bookings.Queries.EstimateBookingFare;
using SafeRide.Application.Features.Bookings.Queries.GetBookingCatalog;
using SafeRide.Contracts.Requests.Bookings;
using SafeRide.Contracts.Responses.Bookings;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly ISender _sender;

    public BookingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("catalog")]
    [ProducesResponseType<BookingCatalogResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BookingCatalogResponse>> GetCatalog(
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        var result = await _sender.Send(
            new GetBookingCatalogQuery(customerId),
            cancellationToken);

        return Ok(new BookingCatalogResponse(
            result.Services
                .Select(service => new BookingServiceOptionResponse(
                    service.Id,
                    service.Name,
                    service.Mode,
                    service.Description))
                .ToList(),
            result.Vehicles
                .Select(vehicle => new BookingVehicleOptionResponse(
                    vehicle.Id,
                    vehicle.Name,
                    vehicle.PlateNumber,
                    vehicle.Color,
                    vehicle.IsMotorbike))
                .ToList()));
    }

    [HttpPost("estimate")]
    [ProducesResponseType<BookingFareEstimateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<BookingFareEstimateResponse>> EstimateFare(
        [FromBody] EstimateBookingFareRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        var result = await _sender.Send(
            new EstimateBookingFareQuery(
                customerId,
                request.VehicleId,
                request.ServiceTypeId,
                request.PickupLatitude,
                request.PickupLongitude,
                request.DestinationLatitude,
                request.DestinationLongitude,
                request.EstimatedHours),
            cancellationToken);

        return Ok(new BookingFareEstimateResponse(
            result.EstimatedDistanceKm,
            result.EstimatedDurationMinutes,
            result.EncodedPolyline,
            result.EstimatedFare));
    }

    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<BookingResponse>> CreateBooking(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        var result = await _sender.Send(
            new CreateBookingCommand(
                customerId,
                request.VehicleId,
                request.ServiceTypeId,
                request.BookingType,
                request.ScheduledAt,
                request.PickupAddress,
                request.PickupLatitude,
                request.PickupLongitude,
                request.DestinationAddress,
                request.DestinationLatitude,
                request.DestinationLongitude,
                request.SpecialRequest,
                request.EstimatedHours),
            cancellationToken);

        var response = ToResponse(
            result.BookingId,
            result.BookingType,
            result.BookingStatus,
            result.ScheduledAt,
            result.EstimatedDistanceKm,
            result.EstimatedDurationMinutes,
            result.EstimatedFare,
            result.EncodedPolyline,
            result.Message,
            result.DriverOffer);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("{bookingId:long}/confirm-driver")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> ConfirmDriver(
        long bookingId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        var result = await _sender.Send(
            new ConfirmDriverCommand(customerId, bookingId),
            cancellationToken);

        return Ok(ToResponse(
            result.BookingId,
            result.BookingType,
            result.BookingStatus,
            result.ScheduledAt,
            result.EstimatedDistanceKm,
            result.EstimatedDurationMinutes,
            result.EstimatedFare,
            result.EncodedPolyline,
            result.Message,
            result.DriverOffer));
    }

    [HttpPost("{bookingId:long}/reject-driver")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> RejectDriver(
        long bookingId,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        var result = await _sender.Send(
            new RejectDriverCommand(customerId, bookingId),
            cancellationToken);

        return Ok(ToResponse(
            result.BookingId,
            result.BookingType,
            result.BookingStatus,
            result.ScheduledAt,
            result.EstimatedDistanceKm,
            result.EstimatedDurationMinutes,
            result.EstimatedFare,
            result.EncodedPolyline,
            result.Message,
            result.DriverOffer));
    }

    [HttpPost("{bookingId:long}/cancel")]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> CancelBooking(
        long bookingId,
        [FromBody] CancelBookingRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        var result = await _sender.Send(
            new CancelBookingCommand(
                customerId,
                bookingId,
                request?.Reason),
            cancellationToken);

        return Ok(ToResponse(
            result.BookingId,
            result.BookingType,
            result.BookingStatus,
            result.ScheduledAt,
            result.EstimatedDistanceKm,
            result.EstimatedDurationMinutes,
            result.EstimatedFare,
            result.EncodedPolyline,
            result.Message,
            result.DriverOffer));
    }

    private static BookingResponse ToResponse(
        long bookingId,
        Domain.Enums.BookingType bookingType,
        Domain.Enums.BookingStatus bookingStatus,
        DateTime? scheduledAt,
        double estimatedDistanceKm,
        int estimatedDurationMinutes,
        decimal estimatedFare,
        string? encodedPolyline,
        string message,
        BookingDriverOfferDto? driverOffer)
    {
        return new BookingResponse(
            bookingId,
            bookingType,
            bookingStatus,
            scheduledAt,
            estimatedDistanceKm,
            estimatedDurationMinutes,
            estimatedFare,
            encodedPolyline,
            message,
            driverOffer is null
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
                    driverOffer.ExpiresAt));
    }

    private bool TryGetCustomerId(out Guid customerId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out customerId);
    }

    private static ProblemDetails CreateUnauthorizedProblem()
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Không xác định được tài khoản khách hàng."
        };
    }
}

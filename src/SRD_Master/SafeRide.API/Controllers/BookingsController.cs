using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
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

        var response = new BookingResponse(
            result.BookingId,
            result.BookingType,
            result.BookingStatus,
            result.ScheduledAt,
            result.EstimatedDistanceKm,
            result.EstimatedDurationMinutes,
            result.EstimatedFare,
            result.EncodedPolyline,
            result.Message);

        return StatusCode(StatusCodes.Status201Created, response);
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

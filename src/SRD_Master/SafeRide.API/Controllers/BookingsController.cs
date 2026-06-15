using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
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

    [HttpPost]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<BookingResponse>> CreateBooking(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var customerIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(customerIdValue, out var customerId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Không xác định được tài khoản khách hàng."
            });
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
                request.SpecialRequest),
            cancellationToken);

        var response = new BookingResponse(
            result.BookingId,
            result.BookingType,
            result.BookingStatus,
            result.ScheduledAt,
            result.EstimatedFare,
            result.Message);

        return StatusCode(StatusCodes.Status201Created, response);
    }
}

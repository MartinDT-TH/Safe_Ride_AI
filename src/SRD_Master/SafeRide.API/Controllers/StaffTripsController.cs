using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.AdminTrips;
using SafeRide.Application.Features.AdminTrips.Queries.GetAdminTripDetails;
using SafeRide.Application.Features.AdminTrips.Queries.GetAdminTripDetailsByBooking;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Contracts.Requests.Trips;
using System.Security.Claims;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/staff/trips")]
public sealed class StaffTripsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITripStatusService _tripStatusService;

    public StaffTripsController(ISender sender, ITripStatusService tripStatusService)
    {
        _sender = sender;
        _tripStatusService = tripStatusService;
    }

    [HttpPost("{tripId:long}/end-reconciliations/{requestId:long}/resolve")]
    [ProducesResponseType<TripEndReconciliationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TripEndReconciliationResult>> ResolveEndReconciliation(
        long tripId,
        long requestId,
        [FromBody] ResolveTripEndReconciliationRequest request,
        CancellationToken cancellationToken)
    {
        var staffUserId = Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException();
        return Ok(await _tripStatusService.ResolveEndTripReconciliationAsync(
            staffUserId,
            tripId,
            requestId,
            request.Approved,
            request.ResolutionNote,
            cancellationToken));
    }

    [HttpGet("{tripId:long}")]
    [ProducesResponseType<AdminTripDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminTripDetailsResponse>> GetTripDetails(
        long tripId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetAdminTripDetailsQuery(tripId),
            cancellationToken);

        return result is null
            ? NotFound(CreateNotFoundProblem("Khong tim thay thong tin chuyen di."))
            : Ok(result);
    }

    [HttpGet("by-booking/{bookingId:long}")]
    [ProducesResponseType<AdminTripDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminTripDetailsResponse>> GetTripDetailsByBooking(
        long bookingId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetAdminTripDetailsByBookingQuery(bookingId),
            cancellationToken);

        return result is null
            ? NotFound(CreateNotFoundProblem("Yeu cau dat xe nay chua co chuyen di."))
            : Ok(result);
    }

    private ProblemDetails CreateNotFoundProblem(string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "staff_trip.not_found";
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return problem;
    }
}

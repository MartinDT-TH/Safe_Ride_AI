using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.AdminBookings.Queries.GetAdminBookings;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/bookings")]
public sealed class AdminBookingsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminBookingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetBookings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetAdminBookingsQuery(
                page,
                pageSize,
                search,
                status,
                sortBy,
                sortDirection,
                fromDate,
                toDate),
            cancellationToken);

        return Ok(result);
    }
}

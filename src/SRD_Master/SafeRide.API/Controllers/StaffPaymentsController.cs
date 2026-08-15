using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.StaffPayments;
using SafeRide.Application.Features.StaffPayments.Queries.GetStaffPaymentStatuses;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/staff/payments")]
public sealed class StaffPaymentsController : ControllerBase
{
    private readonly ISender _sender;

    public StaffPaymentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType<StaffPaymentStatusPagedResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffPaymentStatusPagedResult>> GetPaymentStatuses(
        [FromQuery] string? status,
        [FromQuery] string? method,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetStaffPaymentStatusesQuery(page, pageSize, status, method, fromDate, toDate),
            cancellationToken);

        return Ok(result);
    }
}

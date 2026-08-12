using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Ratings.Queries.GetDriverRatings;
using SafeRide.Contracts.Responses.Feedbacks;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/staff/drivers")]
public sealed class StaffDriverRatingsController : ControllerBase
{
    private readonly ISender _sender;

    public StaffDriverRatingsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("{driverId:guid}/ratings")]
    [ProducesResponseType<DriverRatingSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverRatingSummaryResponse>> GetDriverRatings(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new GetDriverRatingsQuery(driverId),
            cancellationToken);

        return Ok(response);
    }
}

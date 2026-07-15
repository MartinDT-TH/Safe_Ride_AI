using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.TripSharing;
using SafeRide.Contracts.Requests.Trips;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/trip-shares")]
public sealed class TripSharesController : ControllerBase
{
    private readonly ITripSharingService _tripSharingService;

    public TripSharesController(ITripSharingService tripSharingService)
    {
        _tripSharingService = tripSharingService;
    }

    [HttpPost("resolve")]
    [ProducesResponseType<ResolveTripShareResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ResolveTripShareResult>> Resolve(
        [FromBody] ResolveTripShareRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _tripSharingService.ResolveAsync(
            request.Token,
            userId,
            cancellationToken));
    }

    [HttpGet("{tripShareId:long}/tracking")]
    [ProducesResponseType<SharedTripTrackingDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SharedTripTrackingDto>> GetTracking(
        long tripShareId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _tripSharingService.GetTrackingAsync(
            tripShareId,
            userId,
            cancellationToken));
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}

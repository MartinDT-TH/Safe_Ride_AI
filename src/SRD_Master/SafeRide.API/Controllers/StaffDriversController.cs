using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.AdminDrivers.Commands.ReviewAdminDriverKyc;
using SafeRide.Application.Features.AdminDrivers.Queries.GetAdminDrivers;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/staff/drivers")]
public sealed class StaffDriversController : ControllerBase
{
    private readonly ISender _sender;

    public StaffDriversController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetDrivers(
        [FromQuery] string status = "all",
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetAdminDriversQuery(status), cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{driverId:guid}/kyc")]
    public async Task<IActionResult> ReviewKyc(
        Guid driverId,
        [FromBody] StaffReviewKycRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReviewAdminDriverKycCommand(driverId, request.Status, request.RejectionReason),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record StaffReviewKycRequest(KycStatus Status, string? RejectionReason);

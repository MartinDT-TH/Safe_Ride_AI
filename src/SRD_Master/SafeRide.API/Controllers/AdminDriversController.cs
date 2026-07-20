using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.AdminDrivers.Commands.BlockAdminDriver;
using SafeRide.Application.Features.AdminDrivers.Commands.ReviewAdminDriverKyc;
using SafeRide.Application.Features.AdminDrivers.Commands.UnlockAdminDriver;
using SafeRide.Application.Features.AdminDrivers.Queries.GetAdminDrivers;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/drivers")]
public sealed class AdminDriversController : ControllerBase
{
    private readonly ISender _sender;

    public AdminDriversController(ISender sender)
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

    [HttpPatch("{driverId:guid}/block")]
    public async Task<IActionResult> Block(
        Guid driverId,
        [FromBody] BlockDriverRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new BlockAdminDriverCommand(driverId, request.Reason),
            cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{driverId:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid driverId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UnlockAdminDriverCommand(driverId),
            cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{driverId:guid}/kyc")]
    public async Task<IActionResult> ReviewKyc(
        Guid driverId,
        [FromBody] ReviewKycRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReviewAdminDriverKycCommand(driverId, request.Status, request.RejectionReason),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record BlockDriverRequest(string? Reason);

public sealed record ReviewKycRequest(KycStatus Status, string? RejectionReason);

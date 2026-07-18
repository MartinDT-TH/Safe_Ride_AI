using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.AdminCustomers.Commands.BlockAdminCustomer;
using SafeRide.Application.Features.AdminCustomers.Commands.UnlockAdminCustomer;
using SafeRide.Application.Features.AdminCustomers.Queries.GetAdminCustomers;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/customers")]
public sealed class AdminCustomersController : ControllerBase
{
    private readonly ISender _sender;

    public AdminCustomersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(new GetAdminCustomersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{customerId:guid}/block")]
    public async Task<IActionResult> Block(
        Guid customerId,
        [FromBody] BlockCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new BlockAdminCustomerCommand(customerId, request.Reason),
            cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{customerId:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid customerId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UnlockAdminCustomerCommand(customerId),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record BlockCustomerRequest(string? Reason);

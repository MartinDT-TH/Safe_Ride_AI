using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.RiskProtection;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Driver")]
[Route("api/drivers/liabilities")]
public sealed class DriverLiabilitiesController : ControllerBase
{
    private readonly IAccidentManagementService _service;
    public DriverLiabilitiesController(IAccidentManagementService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DriverLiabilityResponse>>> Get(CancellationToken cancellationToken)
        => Ok(await _service.GetDriverLiabilitiesAsync(GetUserId(), cancellationToken));

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException();
}

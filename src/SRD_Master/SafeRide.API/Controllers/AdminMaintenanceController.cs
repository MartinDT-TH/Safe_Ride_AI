using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Infrastructure.Services;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/maintenance")]
public sealed class AdminMaintenanceController(DriverKycBackfillService backfill) : ControllerBase
{
    [HttpPost("driver-kyc-backfill")]
    public async Task<IActionResult> BackfillDriverKyc(CancellationToken cancellationToken)
    {
        var updated = await backfill.RunAsync(cancellationToken);
        return Ok(new { updatedRows = updated });
    }
}

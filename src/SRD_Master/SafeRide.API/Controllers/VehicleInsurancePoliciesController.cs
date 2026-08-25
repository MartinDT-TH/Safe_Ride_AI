using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.RiskProtection;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/vehicles/{vehicleId:long}/insurance-policies")]
public sealed class VehicleInsurancePoliciesController : ControllerBase
{
    private readonly IVehicleInsurancePolicyService _service;
    public VehicleInsurancePoliciesController(IVehicleInsurancePolicyService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleInsurancePolicyResponse>>> Get(
        long vehicleId, CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(GetUserId(), vehicleId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<VehicleInsurancePolicyResponse>> Create(
        long vehicleId, [FromBody] VehicleInsurancePolicyRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(GetUserId(), vehicleId, request, cancellationToken);
        return Created($"/api/vehicles/{vehicleId}/insurance-policies/{result.Id}", result);
    }

    [HttpPut("{policyId:long}")]
    public async Task<ActionResult<VehicleInsurancePolicyResponse>> Update(
        long vehicleId, long policyId, [FromBody] VehicleInsurancePolicyRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(
            GetUserId(), vehicleId, policyId, request, cancellationToken));

    [HttpDelete("{policyId:long}")]
    public async Task<IActionResult> Delete(long vehicleId, long policyId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(GetUserId(), vehicleId, policyId, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException();
}

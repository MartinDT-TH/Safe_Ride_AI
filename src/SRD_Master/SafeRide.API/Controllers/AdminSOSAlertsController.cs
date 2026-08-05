using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.AdminSOSAlerts;
using SafeRide.Application.Features.AdminSOSAlerts.Queries.GetAdminSOSAlertDetails;
using SafeRide.Application.Features.AdminSOSAlerts.Queries.GetAdminSOSAlerts;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/sos-alerts")]
public sealed class AdminSOSAlertsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminSOSAlertsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType<AdminSOSAlertPagedResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminSOSAlertPagedResult>> GetAlerts(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetAdminSOSAlertsQuery(status, page, pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{sosAlertId:long}")]
    [ProducesResponseType<AdminSOSAlertResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminSOSAlertResponse>> GetAlertDetails(
        long sosAlertId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetAdminSOSAlertDetailsQuery(sosAlertId),
            cancellationToken);
        if (result is not null)
        {
            return Ok(result);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = "Không tìm thấy cảnh báo SOS.",
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "sos.alert_not_found";
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return NotFound(problem);
    }
}

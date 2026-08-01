using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.AdminReports.Commands.UpdateAdminReportStatus;
using SafeRide.Application.Features.AdminReports.Queries.GetAdminReportDetails;
using SafeRide.Application.Features.AdminReports.Queries.GetAdminReports;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/reports")]
public sealed class AdminReportsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminReportsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetAdminReportsQuery(page, pageSize, status, search),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{reportId:long}")]
    public async Task<IActionResult> GetReportDetails(
        long reportId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new GetAdminReportDetailsQuery(reportId),
            cancellationToken);
        return Ok(result);
    }

    [HttpPut("{reportId:long}/status")]
    public async Task<IActionResult> UpdateStatus(
        long reportId,
        [FromBody] UpdateAdminReportStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateAdminReportStatusCommand(reportId, request.Status),
            cancellationToken);
        return Ok(result);
    }
}

public sealed record UpdateAdminReportStatusRequest(string Status);

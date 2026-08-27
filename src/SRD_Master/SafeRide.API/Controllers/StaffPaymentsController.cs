using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.StaffPayments;
using SafeRide.Application.Features.StaffPayments.Queries.GetStaffPaymentStatuses;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.RiskProtection;
using System.Security.Claims;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/staff/payments")]
public sealed class StaffPaymentsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ISafetyPaymentReconciliationService _reconciliationService;

    public StaffPaymentsController(
        ISender sender,
        ISafetyPaymentReconciliationService reconciliationService)
    {
        _sender = sender;
        _reconciliationService = reconciliationService;
    }

    [HttpGet]
    [ProducesResponseType<StaffPaymentStatusPagedResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffPaymentStatusPagedResult>> GetPaymentStatuses(
        [FromQuery] string? status,
        [FromQuery] string? method,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetStaffPaymentStatusesQuery(page, pageSize, status, method, fromDate, toDate),
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("refunds/{refundId:long}/confirm")]
    [ProducesResponseType<SafetyPaymentReconciliationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SafetyPaymentReconciliationResponse>> ConfirmManualRefund(
        long refundId,
        [FromBody] ManualRefundConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : throw new UnauthorizedAccessException();
        return Ok(await _reconciliationService.ConfirmManualRefundAsync(
            userId, refundId, request, cancellationToken));
    }

    [HttpGet("refunds")]
    [ProducesResponseType<IReadOnlyList<ManualRefundQueueItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ManualRefundQueueItemResponse>>> GetRefunds(
        [FromQuery] ManualRefundStatus? status,
        CancellationToken cancellationToken) =>
        Ok(await _reconciliationService.ListRefundsAsync(status, cancellationToken));
}

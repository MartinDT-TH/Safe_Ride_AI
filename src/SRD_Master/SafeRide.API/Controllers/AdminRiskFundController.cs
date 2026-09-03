using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminRiskFundController : ControllerBase
{
    private readonly IRiskFundLedgerService _ledger;
    private readonly IRiskProtectionPolicyProvider _policyProvider;

    public AdminRiskFundController(IRiskFundLedgerService ledger, IRiskProtectionPolicyProvider policyProvider)
    {
        _ledger = ledger;
        _policyProvider = policyProvider;
    }

    [HttpGet("risk-fund")]
    public async Task<ActionResult<RiskFundDashboardResponse>> GetDashboard(CancellationToken cancellationToken)
        => Ok(await _ledger.GetDashboardAsync(cancellationToken));

    [HttpGet("risk-fund/transactions")]
    public async Task<ActionResult<RiskFundTransactionPageResponse>> GetTransactions(
        [FromQuery] RiskFundTransactionType? type,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken,
        [FromQuery] int limit = 50,
        [FromQuery] DateTime? cursorCreatedAtUtc = null,
        [FromQuery] long? cursorId = null)
        => Ok(await _ledger.GetTransactionsAsync(
            type, fromUtc, toUtc, limit, cursorCreatedAtUtc, cursorId, cancellationToken));

    [HttpGet("risk-fund/transactions/export")]
    public async Task ExportTransactions(
        [FromQuery] RiskFundTransactionType? type,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition =
            $"attachment; filename=\"risk-fund-{DateTime.UtcNow:yyyy-MM-dd}.csv\"";
        await _ledger.ExportTransactionsAsync(
            type, fromUtc, toUtc, Response.Body, cancellationToken);
    }

    [HttpPost("risk-fund/opening-balance")]
    public async Task<ActionResult<RiskFundMutationResponse>> OpeningBalance(
        [FromBody] RiskFundMutationRequest request, CancellationToken cancellationToken)
        => Ok(await _ledger.ApplyOpeningBalanceAsync(GetUserId(), request, cancellationToken));

    [HttpPost("risk-fund/adjustments")]
    public async Task<ActionResult<RiskFundMutationResponse>> Adjustment(
        [FromBody] RiskFundMutationRequest request, CancellationToken cancellationToken)
        => Ok(await _ledger.ApplyAdjustmentAsync(GetUserId(), request, cancellationToken));

    [HttpGet("risk-protection/configuration")]
    public async Task<ActionResult<RiskProtectionPolicyResponse>> GetConfiguration(CancellationToken cancellationToken)
    {
        var current = await _policyProvider.GetCurrentAsync(cancellationToken);
        return current is null ? NotFound() : Ok(current);
    }

    [HttpGet("risk-protection/configuration/versions")]
    public async Task<ActionResult<IReadOnlyList<RiskProtectionPolicyResponse>>> GetConfigurationVersions(
        CancellationToken cancellationToken) =>
        Ok(await _policyProvider.ListAsync(cancellationToken));

    [HttpPut("risk-protection/configuration")]
    public async Task<ActionResult<RiskProtectionPolicyResponse>> CreateConfiguration(
        [FromBody] CreateRiskProtectionPolicyRequest request, CancellationToken cancellationToken)
        => Ok(await _policyProvider.CreateAsync(GetUserId(), request, cancellationToken));

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException();
}

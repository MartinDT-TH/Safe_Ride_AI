using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.AccountBans.Commands.UpdateAccountBanConfiguration;
using SafeRide.Application.Features.AccountBans.DTOs;
using SafeRide.Application.Features.AccountBans.Queries.GetAccountBanConfiguration;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/account-ban-configuration")]
public sealed class AdminAccountBanConfigurationController : ControllerBase
{
    private readonly ISender _sender;

    public AdminAccountBanConfigurationController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType<AccountBanConfigurationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AccountBanConfigurationResponse>> GetConfiguration(
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            new GetAccountBanConfigurationQuery(),
            cancellationToken);
        return Ok(response);
    }

    [HttpPut]
    [ProducesResponseType<AccountBanConfigurationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AccountBanConfigurationResponse>> UpdateConfiguration(
        [FromBody] UpdateAccountBanConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var adminUserId))
        {
            return Unauthorized();
        }

        var response = await _sender.Send(
            new UpdateAccountBanConfigurationCommand(
                request.NegativeFeedbackThreshold,
                request.NegativeRatingMaxScore,
                request.TemporaryBanDurationDays,
                request.MaximumTemporaryBans,
                request.IsEnabled,
                adminUserId),
            cancellationToken);
        return Ok(response);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }
}

public sealed record UpdateAccountBanConfigurationRequest(
    int NegativeFeedbackThreshold,
    int NegativeRatingMaxScore,
    int TemporaryBanDurationDays,
    int MaximumTemporaryBans,
    bool IsEnabled);

using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Promotions.Queries.GetAvailablePromotions;
using SafeRide.Contracts.Responses.Promotions;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/promotions")]
public sealed class PromotionsController : ControllerBase
{
    private readonly ISender _sender;

    public PromotionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("available")]
    [ProducesResponseType<IReadOnlyList<AvailablePromotionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AvailablePromotionResponse>>> GetAvailable(
        CancellationToken cancellationToken)
    {
        if (!TryGetCustomerId(out var customerId))
        {
            return Unauthorized(CreateUnauthorizedProblem());
        }

        var result = await _sender.Send(
            new GetAvailablePromotionsQuery(customerId),
            cancellationToken);

        return Ok(result);
    }

    private bool TryGetCustomerId(out Guid customerId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out customerId);
    }

    private static ProblemDetails CreateUnauthorizedProblem()
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Không xác định được tài khoản khách hàng."
        };
    }
}

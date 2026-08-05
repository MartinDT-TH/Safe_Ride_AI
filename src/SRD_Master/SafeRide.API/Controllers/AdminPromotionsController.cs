using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Promotions.Commands.CreateAdminPromotion;
using SafeRide.Application.Features.Promotions.Commands.UpdateAdminPromotion;
using SafeRide.Application.Features.Promotions.Queries.GetAdminPromotions;
using SafeRide.Contracts.Requests.Promotions;
using SafeRide.Contracts.Responses.Promotions;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/promotions")]
public sealed class AdminPromotionsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminPromotionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType<AdminPromotionsPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminPromotionsPageResponse>> GetPromotions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = "all",
        CancellationToken cancellationToken = default)
    {
        var response = await _sender.Send(
            new GetAdminPromotionsQuery(page, pageSize, search, status),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType<AdminPromotionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminPromotionResponse>> CreatePromotion(
        [FromBody] AdminPromotionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            ToCreateCommand(request),
            cancellationToken);

        return Created($"/api/admin/promotions/{response.Id}", response);
    }

    [HttpPut("{promotionId:long}")]
    [ProducesResponseType<AdminPromotionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminPromotionResponse>> UpdatePromotion(
        long promotionId,
        [FromBody] AdminPromotionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            ToUpdateCommand(promotionId, request),
            cancellationToken);

        return Ok(response);
    }

    private static CreateAdminPromotionCommand ToCreateCommand(
        AdminPromotionRequest request)
    {
        return new CreateAdminPromotionCommand(
            request.PromotionCode,
            request.DiscountType,
            request.DiscountValue,
            request.StartDate,
            request.EndDate,
            request.MaxUsageCount,
            request.MinimumOrderValue,
            request.MaximumDiscountValue,
            request.UsageLimitPerUser,
            request.IsActive);
    }

    private static UpdateAdminPromotionCommand ToUpdateCommand(
        long promotionId,
        AdminPromotionRequest request)
    {
        return new UpdateAdminPromotionCommand(
            promotionId,
            request.PromotionCode,
            request.DiscountType,
            request.DiscountValue,
            request.StartDate,
            request.EndDate,
            request.MaxUsageCount,
            request.MinimumOrderValue,
            request.MaximumDiscountValue,
            request.UsageLimitPerUser,
            request.IsActive);
    }
}

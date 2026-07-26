using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Features.Pricing.Commands.CreateAdminPricingRule;
using SafeRide.Application.Features.Pricing.Commands.UpdateAdminPricingRule;
using SafeRide.Application.Features.Pricing.Queries.GetAdminPricingRules;
using SafeRide.Contracts.Requests.Pricing;
using SafeRide.Contracts.Responses.Pricing;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/pricing-rules")]
public sealed class AdminPricingRulesController : ControllerBase
{
    private readonly ISender _sender;

    public AdminPricingRulesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [ProducesResponseType<AdminPricingRulesPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminPricingRulesPageResponse>> GetPricingRules(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = "all",
        CancellationToken cancellationToken = default)
    {
        var response = await _sender.Send(
            new GetAdminPricingRulesQuery(page, pageSize, search, status),
            cancellationToken);

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType<AdminPricingRuleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminPricingRuleResponse>> CreatePricingRule(
        [FromBody] AdminPricingRuleRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            ToCreateCommand(request),
            cancellationToken);

        return Created($"/api/admin/pricing-rules/{response.Id}", response);
    }

    [HttpPut("{pricingRuleId:long}")]
    [ProducesResponseType<AdminPricingRuleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPricingRuleResponse>> UpdatePricingRule(
        long pricingRuleId,
        [FromBody] AdminPricingRuleRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _sender.Send(
            ToUpdateCommand(pricingRuleId, request),
            cancellationToken);

        return Ok(response);
    }

    private static CreateAdminPricingRuleCommand ToCreateCommand(
        AdminPricingRuleRequest request)
    {
        return new CreateAdminPricingRuleCommand(
            request.VehicleClass,
            request.ServiceTypeId,
            request.BaseFare,
            request.MinFare,
            request.PricePerKm,
            request.PricePerHour,
            request.IsActive);
    }

    private static UpdateAdminPricingRuleCommand ToUpdateCommand(
        long pricingRuleId,
        AdminPricingRuleRequest request)
    {
        return new UpdateAdminPricingRuleCommand(
            pricingRuleId,
            request.VehicleClass,
            request.ServiceTypeId,
            request.BaseFare,
            request.MinFare,
            request.PricePerKm,
            request.PricePerHour,
            request.IsActive);
    }
}

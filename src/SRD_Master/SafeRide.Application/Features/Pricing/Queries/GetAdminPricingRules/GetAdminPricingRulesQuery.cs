using MediatR;
using SafeRide.Contracts.Responses.Pricing;

namespace SafeRide.Application.Features.Pricing.Queries.GetAdminPricingRules;

public sealed record GetAdminPricingRulesQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Status) : IRequest<AdminPricingRulesPageResponse>;

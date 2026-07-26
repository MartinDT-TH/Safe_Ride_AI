using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Pricing;

namespace SafeRide.Application.Features.Pricing.Queries.GetAdminPricingRules;

public sealed class GetAdminPricingRulesQueryHandler
    : IRequestHandler<GetAdminPricingRulesQuery, AdminPricingRulesPageResponse>
{
    private static readonly HashSet<string> SupportedStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "all",
            "active",
            "inactive"
        };

    private readonly IAdminPricingRuleRepository _pricingRuleRepository;

    public GetAdminPricingRulesQueryHandler(
        IAdminPricingRuleRepository pricingRuleRepository)
    {
        _pricingRuleRepository = pricingRuleRepository;
    }

    public async Task<AdminPricingRulesPageResponse> Handle(
        GetAdminPricingRulesQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = request.Search?.Trim();
        var status = string.IsNullOrWhiteSpace(request.Status)
            ? "all"
            : request.Status.Trim().ToLowerInvariant();

        if (!SupportedStatuses.Contains(status))
        {
            throw new PricingRuleException(
                "admin_pricing_rule.invalid_status",
                "Trạng thái cấu hình giá không hợp lệ.",
                400);
        }

        var data = await _pricingRuleRepository.GetAdminPricingRulesAsync(
            page,
            pageSize,
            search,
            status,
            cancellationToken);

        return new AdminPricingRulesPageResponse(
            data.Items.Select(AdminPricingRuleRules.ToResponse).ToList(),
            new AdminPricingRuleCountsResponse(
                data.Total,
                data.Active,
                data.Inactive,
                data.MostCommonVehicleClass,
                data.LastUpdatedAt),
            data.ServiceTypes
                .Select(AdminPricingRuleRules.ToServiceTypeResponse)
                .ToList(),
            page,
            pageSize,
            data.TotalItems,
            Math.Max(1, (int)Math.Ceiling(data.TotalItems / (double)pageSize)));
    }
}

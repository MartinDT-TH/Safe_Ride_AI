using SafeRide.Application.Features.Pricing.DTOs;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Common.Interfaces;

public interface IAdminPricingRuleRepository
{
    Task<AdminPricingRulesPageData> GetAdminPricingRulesAsync(
        int page,
        int pageSize,
        string? search,
        string status,
        CancellationToken cancellationToken);

    Task<PricingRule?> GetByIdAsync(
        long pricingRuleId,
        CancellationToken cancellationToken);

    Task<ServiceType?> GetServiceTypeAsync(
        long serviceTypeId,
        CancellationToken cancellationToken);

    Task AddAsync(
        PricingRule pricingRule,
        CancellationToken cancellationToken);

    Task InvalidateActivePricingRulesCacheAsync(
        CancellationToken cancellationToken);
}

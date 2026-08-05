using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Pricing.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Repositories;

public sealed class AdminPricingRuleRepository : IAdminPricingRuleRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;

    public AdminPricingRuleRepository(
        ApplicationDbContext dbContext,
        IRedisService redisService)
    {
        _dbContext = dbContext;
        _redisService = redisService;
    }

    public async Task<AdminPricingRulesPageData> GetAdminPricingRulesAsync(
        int page,
        int pageSize,
        string? search,
        string status,
        CancellationToken cancellationToken)
    {
        var summaryQuery = _dbContext.PricingRules.AsNoTracking();
        var total = await summaryQuery.CountAsync(cancellationToken);
        var active = await summaryQuery.CountAsync(
            pricingRule => pricingRule.IsActive,
            cancellationToken);
        var inactive = await summaryQuery.CountAsync(
            pricingRule => !pricingRule.IsActive,
            cancellationToken);
        var lastUpdatedAt = await summaryQuery
            .Select(pricingRule => (DateTime?)(pricingRule.UpdatedAt ?? pricingRule.CreatedAt))
            .MaxAsync(cancellationToken);
        var mostCommonVehicleClass = await summaryQuery
            .GroupBy(pricingRule => pricingRule.VehicleClass)
            .OrderByDescending(group => group.Count())
            .Select(group => (RequiredLicenseClass?)group.Key)
            .FirstOrDefaultAsync(cancellationToken);

        IQueryable<PricingRule> query = _dbContext.PricingRules
            .AsNoTracking()
            .Include(pricingRule => pricingRule.ServiceType);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var matchingVehicleClasses = Enum.GetValues<RequiredLicenseClass>()
                .Where(vehicleClass => vehicleClass
                    .ToString()
                    .Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = matchingVehicleClasses.Length == 0
                ? query.Where(pricingRule =>
                    pricingRule.ServiceType.ServiceName.Contains(normalizedSearch))
                : query.Where(pricingRule =>
                    pricingRule.ServiceType.ServiceName.Contains(normalizedSearch)
                    || matchingVehicleClasses.Contains(pricingRule.VehicleClass));
        }

        query = status switch
        {
            "active" => query.Where(pricingRule => pricingRule.IsActive),
            "inactive" => query.Where(pricingRule => !pricingRule.IsActive),
            _ => query
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(pricingRule => pricingRule.VehicleClass)
            .ThenBy(pricingRule => pricingRule.ServiceType.ServiceName)
            .ThenByDescending(pricingRule => pricingRule.CreatedAt)
            .ThenByDescending(pricingRule => pricingRule.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var serviceTypes = await _dbContext.ServiceTypes
            .AsNoTracking()
            .OrderBy(serviceType => serviceType.Id)
            .ToListAsync(cancellationToken);

        return new AdminPricingRulesPageData(
            items,
            serviceTypes,
            total,
            active,
            inactive,
            totalItems,
            mostCommonVehicleClass,
            lastUpdatedAt);
    }

    public Task<PricingRule?> GetByIdAsync(
        long pricingRuleId,
        CancellationToken cancellationToken)
    {
        return _dbContext.PricingRules
            .Include(pricingRule => pricingRule.ServiceType)
            .FirstOrDefaultAsync(
                pricingRule => pricingRule.Id == pricingRuleId,
                cancellationToken);
    }

    public Task<ServiceType?> GetServiceTypeAsync(
        long serviceTypeId,
        CancellationToken cancellationToken)
    {
        return _dbContext.ServiceTypes
            .FirstOrDefaultAsync(
                serviceType => serviceType.Id == serviceTypeId,
                cancellationToken);
    }

    public async Task AddAsync(
        PricingRule pricingRule,
        CancellationToken cancellationToken)
    {
        await _dbContext.PricingRules.AddAsync(pricingRule, cancellationToken);
    }

    public async Task InvalidateActivePricingRulesCacheAsync(
        CancellationToken cancellationToken)
    {
        await _redisService.RemoveAsync(RedisKeys.ActivePricingRules);
    }
}

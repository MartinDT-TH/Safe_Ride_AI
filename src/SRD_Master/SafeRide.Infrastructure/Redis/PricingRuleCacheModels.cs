using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Redis;

public sealed record PricingRuleCacheItem(
    long Id,
    RequiredLicenseClass VehicleClass,
    long ServiceTypeId,
    string ServiceTypeName,
    decimal BaseFare,
    decimal MinFare,
    decimal? PricePerKm,
    decimal? PricePerHour,
    DateTime CreatedAt);

public static class PricingRuleCacheItemExtensions
{
    public static PricingRule ToEntity(this PricingRuleCacheItem item)
    {
        return new PricingRule
        {
            Id = item.Id,
            VehicleClass = item.VehicleClass,
            ServiceTypeId = item.ServiceTypeId,
            ServiceType = new ServiceType
            {
                Id = item.ServiceTypeId,
                ServiceName = item.ServiceTypeName
            },
            BaseFare = item.BaseFare,
            MinFare = item.MinFare,
            PricePerKm = item.PricePerKm,
            PricePerHour = item.PricePerHour,
            IsActive = true,
            CreatedAt = item.CreatedAt
        };
    }

    public static PricingRuleCacheItem ToCacheItem(this PricingRule pricingRule)
    {
        return new PricingRuleCacheItem(
            pricingRule.Id,
            pricingRule.VehicleClass,
            pricingRule.ServiceTypeId,
            pricingRule.ServiceType.ServiceName,
            pricingRule.BaseFare,
            pricingRule.MinFare,
            pricingRule.PricePerKm,
            pricingRule.PricePerHour,
            pricingRule.CreatedAt);
    }
}

public sealed record SurgePricingRuleCacheItem(
    long Id,
    string RuleName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string AppliedDays,
    decimal SurgeMultiplier,
    DateTime CreatedAt);

public static class SurgePricingRuleCacheItemExtensions
{
    public static SurgePricingRule ToEntity(this SurgePricingRuleCacheItem item)
    {
        return new SurgePricingRule
        {
            Id = item.Id,
            RuleName = item.RuleName,
            StartTime = item.StartTime,
            EndTime = item.EndTime,
            AppliedDays = item.AppliedDays,
            SurgeMultiplier = item.SurgeMultiplier,
            IsActive = true,
            CreatedAt = item.CreatedAt
        };
    }

    public static SurgePricingRuleCacheItem ToCacheItem(this SurgePricingRule rule)
    {
        return new SurgePricingRuleCacheItem(
            rule.Id,
            rule.RuleName,
            rule.StartTime,
            rule.EndTime,
            rule.AppliedDays,
            rule.SurgeMultiplier,
            rule.CreatedAt);
    }
}

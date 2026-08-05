using SafeRide.Contracts.Responses.Pricing;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Pricing;

internal static class AdminPricingRuleRules
{
    public static void Validate(
        RequiredLicenseClass vehicleClass,
        ServiceType? serviceType,
        decimal baseFare,
        decimal minFare,
        decimal? pricePerKm,
        decimal? pricePerHour)
    {
        if (!Enum.IsDefined(typeof(RequiredLicenseClass), vehicleClass))
        {
            throw new PricingRuleException(
                "admin_pricing_rule.invalid_vehicle_class",
                "Hạng xe không hợp lệ.",
                400);
        }

        if (serviceType is null)
        {
            throw new PricingRuleException(
                "admin_pricing_rule.service_type_not_found",
                "Dịch vụ tính giá không tồn tại.",
                400);
        }

        if (baseFare < 0 || minFare < 0)
        {
            throw new PricingRuleException(
                "admin_pricing_rule.invalid_currency_value",
                "Giá cơ bản và giá tối thiểu không được nhỏ hơn 0.",
                400);
        }

        if (pricePerKm.HasValue && pricePerKm.Value < 0)
        {
            throw new PricingRuleException(
                "admin_pricing_rule.invalid_price_per_km",
                "Giá mỗi km không được nhỏ hơn 0.",
                400);
        }

        if (pricePerHour.HasValue && pricePerHour.Value < 0)
        {
            throw new PricingRuleException(
                "admin_pricing_rule.invalid_price_per_hour",
                "Giá mỗi giờ không được nhỏ hơn 0.",
                400);
        }

        if (pricePerKm.HasValue == pricePerHour.HasValue)
        {
            throw new PricingRuleException(
                "admin_pricing_rule.invalid_unit_price",
                "Mỗi cấu hình giá phải có đúng một loại giá theo km hoặc theo giờ.",
                400);
        }
    }

    public static AdminPricingRuleResponse ToResponse(PricingRule pricingRule)
    {
        return new AdminPricingRuleResponse(
            pricingRule.Id,
            pricingRule.VehicleClass,
            pricingRule.ServiceTypeId,
            pricingRule.ServiceType.ServiceName,
            pricingRule.BaseFare,
            pricingRule.MinFare,
            pricingRule.PricePerKm,
            pricingRule.PricePerHour,
            pricingRule.IsActive,
            pricingRule.CreatedAt,
            pricingRule.UpdatedAt);
    }

    public static AdminPricingRuleServiceTypeResponse ToServiceTypeResponse(
        ServiceType serviceType)
    {
        return new AdminPricingRuleServiceTypeResponse(
            serviceType.Id,
            serviceType.ServiceName);
    }
}

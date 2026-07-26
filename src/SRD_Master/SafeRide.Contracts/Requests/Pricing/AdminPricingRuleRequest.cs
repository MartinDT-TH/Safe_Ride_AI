using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Requests.Pricing;

public sealed record AdminPricingRuleRequest(
    RequiredLicenseClass VehicleClass,
    long ServiceTypeId,
    decimal BaseFare,
    decimal MinFare,
    decimal? PricePerKm,
    decimal? PricePerHour,
    bool IsActive);

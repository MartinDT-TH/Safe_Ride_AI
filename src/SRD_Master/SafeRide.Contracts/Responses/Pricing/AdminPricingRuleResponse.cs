using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Responses.Pricing;

public sealed record AdminPricingRuleResponse(
    long Id,
    RequiredLicenseClass VehicleClass,
    long ServiceTypeId,
    string ServiceTypeName,
    decimal BaseFare,
    decimal MinFare,
    decimal? PricePerKm,
    decimal? PricePerHour,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AdminPricingRuleCountsResponse(
    int Total,
    int Active,
    int Inactive,
    RequiredLicenseClass? MostCommonVehicleClass,
    DateTime? LastUpdatedAt);

public sealed record AdminPricingRuleServiceTypeResponse(
    long Id,
    string ServiceName);

public sealed record AdminPricingRulesPageResponse(
    IReadOnlyCollection<AdminPricingRuleResponse> Items,
    AdminPricingRuleCountsResponse Counts,
    IReadOnlyCollection<AdminPricingRuleServiceTypeResponse> ServiceTypes,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

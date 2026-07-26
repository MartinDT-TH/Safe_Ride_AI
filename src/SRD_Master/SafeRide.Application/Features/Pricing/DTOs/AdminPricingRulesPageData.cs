using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Pricing.DTOs;

public sealed record AdminPricingRulesPageData(
    IReadOnlyList<PricingRule> Items,
    IReadOnlyList<ServiceType> ServiceTypes,
    int Total,
    int Active,
    int Inactive,
    int TotalItems,
    RequiredLicenseClass? MostCommonVehicleClass,
    DateTime? LastUpdatedAt);

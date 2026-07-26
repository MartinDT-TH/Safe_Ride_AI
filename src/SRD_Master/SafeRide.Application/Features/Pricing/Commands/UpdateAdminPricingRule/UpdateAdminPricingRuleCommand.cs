using MediatR;
using SafeRide.Contracts.Responses.Pricing;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Pricing.Commands.UpdateAdminPricingRule;

public sealed record UpdateAdminPricingRuleCommand(
    long PricingRuleId,
    RequiredLicenseClass VehicleClass,
    long ServiceTypeId,
    decimal BaseFare,
    decimal MinFare,
    decimal? PricePerKm,
    decimal? PricePerHour,
    bool IsActive) : IRequest<AdminPricingRuleResponse>;

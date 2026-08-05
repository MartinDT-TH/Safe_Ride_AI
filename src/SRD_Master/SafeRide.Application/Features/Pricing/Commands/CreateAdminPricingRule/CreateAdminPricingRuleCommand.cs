using MediatR;
using SafeRide.Contracts.Responses.Pricing;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Pricing.Commands.CreateAdminPricingRule;

public sealed record CreateAdminPricingRuleCommand(
    RequiredLicenseClass VehicleClass,
    long ServiceTypeId,
    decimal BaseFare,
    decimal MinFare,
    decimal? PricePerKm,
    decimal? PricePerHour,
    bool IsActive) : IRequest<AdminPricingRuleResponse>;

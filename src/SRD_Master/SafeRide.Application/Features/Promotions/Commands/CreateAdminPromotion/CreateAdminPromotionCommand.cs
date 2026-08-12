using MediatR;
using SafeRide.Contracts.Responses.Promotions;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Promotions.Commands.CreateAdminPromotion;

public sealed record CreateAdminPromotionCommand(
    string PromotionCode,
    DiscountType? DiscountType,
    decimal DiscountValue,
    DateTime StartDate,
    DateTime EndDate,
    int MaxUsageCount,
    decimal MinimumOrderValue,
    decimal MaximumDiscountValue,
    int UsageLimitPerUser,
    int? RequiredCompletedTrips,
    bool IsActive) : IRequest<AdminPromotionResponse>;

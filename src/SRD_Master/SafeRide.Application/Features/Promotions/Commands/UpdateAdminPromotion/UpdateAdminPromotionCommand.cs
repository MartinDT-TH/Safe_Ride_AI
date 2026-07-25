using MediatR;
using SafeRide.Contracts.Responses.Promotions;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Promotions.Commands.UpdateAdminPromotion;

public sealed record UpdateAdminPromotionCommand(
    long PromotionId,
    string PromotionCode,
    DiscountType? DiscountType,
    decimal DiscountValue,
    DateTime StartDate,
    DateTime EndDate,
    int MaxUsageCount,
    decimal MinimumOrderValue,
    decimal MaximumDiscountValue,
    int UsageLimitPerUser,
    bool IsActive) : IRequest<AdminPromotionResponse>;

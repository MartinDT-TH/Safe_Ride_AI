using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Responses.Promotions;

public sealed record AvailablePromotionResponse(
    long PromotionId,
    string PromotionCode,
    DiscountType? DiscountType,
    decimal DiscountValue,
    DateTime StartDate,
    DateTime EndDate,
    decimal MinimumOrderValue,
    decimal MaximumDiscountValue,
    int UsageLimitPerUser,
    int? RemainingUsageCount,
    string ShortDescription);

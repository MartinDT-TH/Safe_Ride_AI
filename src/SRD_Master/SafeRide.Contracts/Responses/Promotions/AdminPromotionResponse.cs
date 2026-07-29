using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Responses.Promotions;

public sealed record AdminPromotionResponse(
    long Id,
    string PromotionCode,
    DiscountType DiscountType,
    decimal DiscountValue,
    DateTime StartDate,
    DateTime EndDate,
    int MaxUsageCount,
    int CurrentUsageCount,
    decimal MinimumOrderValue,
    decimal MaximumDiscountValue,
    int UsageLimitPerUser,
    bool IsActive);

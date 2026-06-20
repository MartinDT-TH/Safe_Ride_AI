using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Promotions;

internal static class PromotionApplicationRules
{
    public static string NormalizePromotionCode(string? promotionCode)
    {
        var normalized = promotionCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        throw new PromotionException(
            "promotion.code_required",
            "Vui lòng nhập mã khuyến mãi.",
            400);
    }

    public static void ValidateAvailability(
        Promotion promotion,
        DateTime utcNow)
    {
        if (!promotion.IsActive)
        {
            throw new PromotionException(
                "promotion.unavailable",
                "Mã khuyến mãi hiện không khả dụng.",
                400);
        }

        if (utcNow < promotion.StartDate)
        {
            throw new PromotionException(
                "promotion.not_started",
                "Mã khuyến mãi chưa đến thời gian sử dụng.",
                400);
        }

        if (utcNow > promotion.EndDate)
        {
            throw new PromotionException(
                "promotion.expired",
                "Mã khuyến mãi đã hết hạn.",
                400);
        }

        if (promotion.MaxUsageCount <= 0 ||
            promotion.CurrentUsageCount >= promotion.MaxUsageCount)
        {
            throw new PromotionException(
                "promotion.usage_exhausted",
                "Mã khuyến mãi đã hết lượt sử dụng.",
                409);
        }

        if (!promotion.DiscountType.HasValue ||
            !Enum.IsDefined(promotion.DiscountType.Value))
        {
            throw new PromotionException(
                "promotion.invalid_discount_type",
                "Loại khuyến mãi không hợp lệ.",
                400);
        }
    }

    public static void ValidateMinimumOrderValue(
        decimal originalFare,
        decimal minimumOrderValue)
    {
        if (originalFare >= minimumOrderValue)
        {
            return;
        }

        throw new PromotionException(
            "promotion.minimum_order_value_not_met",
            "Đơn hàng chưa đạt giá trị tối thiểu để áp dụng mã.",
            400);
    }

    public static void ValidateCustomerUsageLimit(
        int usageCount,
        int usageLimitPerUser)
    {
        if (usageLimitPerUser <= 0 || usageCount >= usageLimitPerUser)
        {
            throw new PromotionException(
                "promotion.user_usage_limit_exceeded",
                "Bạn đã sử dụng mã khuyến mãi này quá số lần cho phép.",
                409);
        }
    }

    public static decimal CalculateDiscountAmount(
        Promotion promotion,
        decimal originalFare)
    {
        var discountAmount = promotion.DiscountType switch
        {
            DiscountType.Percentage => originalFare * promotion.DiscountValue / 100m,
            DiscountType.Fixed => promotion.DiscountValue,
            _ => throw new PromotionException(
                "promotion.invalid_discount_type",
                "Loại khuyến mãi không hợp lệ.",
                400)
        };

        if (promotion.MaximumDiscountValue > 0)
        {
            discountAmount = Math.Min(discountAmount, promotion.MaximumDiscountValue);
        }

        discountAmount = Math.Min(discountAmount, originalFare);
        if (discountAmount < 0)
        {
            throw new PromotionException(
                "promotion.invalid_discount_value",
                "Giá trị khuyến mãi không hợp lệ.",
                400);
        }

        return discountAmount;
    }
}

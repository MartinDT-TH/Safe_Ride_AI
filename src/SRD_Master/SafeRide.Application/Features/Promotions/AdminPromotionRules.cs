using SafeRide.Contracts.Responses.Promotions;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Promotions;

internal static class AdminPromotionRules
{
    public static string NormalizeCode(string? promotionCode)
    {
        var normalized = promotionCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new PromotionException(
                "admin_promotion.code_required",
                "Vui lòng nhập mã khuyến mãi.",
                400);
        }

        if (normalized.Length > 50)
        {
            throw new PromotionException(
                "admin_promotion.code_too_long",
                "Mã khuyến mãi không được vượt quá 50 ký tự.",
                400);
        }

        return normalized;
    }

    public static void Validate(
        DiscountType? discountType,
        decimal discountValue,
        DateTime startDate,
        DateTime endDate,
        int maxUsageCount,
        decimal minimumOrderValue,
        decimal maximumDiscountValue,
        int usageLimitPerUser)
    {
        if (!discountType.HasValue || !Enum.IsDefined(discountType.Value))
        {
            throw new PromotionException(
                "admin_promotion.invalid_discount_type",
                "Loại khuyến mãi không hợp lệ.",
                400);
        }

        if (discountValue <= 0)
        {
            throw new PromotionException(
                "admin_promotion.invalid_discount_value",
                "Giá trị khuyến mãi phải lớn hơn 0.",
                400);
        }

        if (discountType == DiscountType.Percentage && discountValue > 100)
        {
            throw new PromotionException(
                "admin_promotion.percentage_exceeds_limit",
                "Phần trăm giảm giá không được vượt quá 100.",
                400);
        }

        if (endDate <= startDate)
        {
            throw new PromotionException(
                "admin_promotion.invalid_date_range",
                "Ngày kết thúc phải sau ngày bắt đầu.",
                400);
        }

        if (maxUsageCount <= 0)
        {
            throw new PromotionException(
                "admin_promotion.invalid_max_usage_count",
                "Tổng lượt sử dụng phải lớn hơn 0.",
                400);
        }

        if (minimumOrderValue < 0 || maximumDiscountValue < 0)
        {
            throw new PromotionException(
                "admin_promotion.invalid_order_value",
                "Giá trị đơn tối thiểu và mức giảm tối đa không được nhỏ hơn 0.",
                400);
        }

        if (usageLimitPerUser <= 0)
        {
            throw new PromotionException(
                "admin_promotion.invalid_user_usage_limit",
                "Giới hạn sử dụng mỗi người phải lớn hơn 0.",
                400);
        }
    }

    public static void ValidateMaxUsageForUpdate(
        int maxUsageCount,
        int currentUsageCount)
    {
        if (maxUsageCount < currentUsageCount)
        {
            throw new PromotionException(
                "admin_promotion.max_usage_below_current_usage",
                "Tổng lượt sử dụng không được nhỏ hơn số lượt đã sử dụng.",
                400);
        }
    }

    public static AdminPromotionResponse ToResponse(Promotion promotion)
    {
        return new AdminPromotionResponse(
            promotion.Id,
            promotion.PromotionCode,
            promotion.DiscountType!.Value,
            promotion.DiscountValue,
            promotion.StartDate,
            promotion.EndDate,
            promotion.MaxUsageCount,
            promotion.CurrentUsageCount,
            promotion.MinimumOrderValue,
            promotion.MaximumDiscountValue,
            promotion.UsageLimitPerUser,
            promotion.IsActive);
    }
}

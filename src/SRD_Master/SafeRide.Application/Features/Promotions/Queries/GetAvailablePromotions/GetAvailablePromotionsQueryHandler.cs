using System.Globalization;
using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Promotions;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Promotions.Queries.GetAvailablePromotions;

public sealed class GetAvailablePromotionsQueryHandler
    : IRequestHandler<GetAvailablePromotionsQuery, IReadOnlyList<AvailablePromotionResponse>>
{
    private readonly IPromotionRepository _promotionRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPromotionUnlockRuleStore _unlockRuleStore;

    public GetAvailablePromotionsQueryHandler(
        IPromotionRepository promotionRepository,
        IDateTimeProvider dateTimeProvider,
        IPromotionUnlockRuleStore unlockRuleStore)
    {
        _promotionRepository = promotionRepository;
        _dateTimeProvider = dateTimeProvider;
        _unlockRuleStore = unlockRuleStore;
    }

    public async Task<IReadOnlyList<AvailablePromotionResponse>> Handle(
        GetAvailablePromotionsQuery request,
        CancellationToken cancellationToken)
    {
        var promotions = await _promotionRepository.GetAvailablePromotionsAsync(
            _dateTimeProvider.UtcNow,
            cancellationToken);

        var responses = new List<AvailablePromotionResponse>();
        var requiredTripsByCode = await _unlockRuleStore.GetRequiredCompletedTripsAsync(
            promotions.Select(promotion => promotion.PromotionCode).ToList(),
            cancellationToken);
        var requiresCompletedTrips = requiredTripsByCode.Values.Any(value => value > 0);
        var customerCompletedTrips = requiresCompletedTrips
            ? await _promotionRepository.CountCustomerCompletedTripsAsync(
                request.CustomerId,
                cancellationToken)
            : 0;

        foreach (var promotion in promotions)
        {
            var requiredCompletedTrips = requiredTripsByCode.GetValueOrDefault(
                promotion.PromotionCode);
            var usageCount = await _promotionRepository.CountCustomerPromotionUsageAsync(
                request.CustomerId,
                promotion.Id,
                cancellationToken);

            if (usageCount >= promotion.UsageLimitPerUser)
            {
                continue;
            }

            responses.Add(ToResponse(
                promotion,
                requiredCompletedTrips,
                customerCompletedTrips));
        }

        return responses;
    }

    private static AvailablePromotionResponse ToResponse(
        Promotion promotion,
        int requiredCompletedTrips,
        int customerCompletedTrips)
    {
        var remainingTripsToUnlock = Math.Max(
            0,
            requiredCompletedTrips - customerCompletedTrips);
        var isUnlocked = remainingTripsToUnlock == 0;
        return new AvailablePromotionResponse(
            promotion.Id,
            promotion.PromotionCode,
            promotion.DiscountType,
            promotion.DiscountValue,
            promotion.StartDate,
            promotion.EndDate,
            promotion.MinimumOrderValue,
            promotion.MaximumDiscountValue,
            promotion.UsageLimitPerUser,
            Math.Max(0, promotion.MaxUsageCount - promotion.CurrentUsageCount),
            CreateShortDescription(promotion),
            requiredCompletedTrips,
            customerCompletedTrips,
            remainingTripsToUnlock,
            isUnlocked,
            isUnlocked
                ? null
                : $"Bạn cần hoàn thành thêm {remainingTripsToUnlock} chuyến để sử dụng mã khuyến mãi này.");
    }

    private static string CreateShortDescription(Promotion promotion)
    {
        return promotion.DiscountType switch
        {
            DiscountType.Percentage when promotion.MaximumDiscountValue > 0 =>
                $"Giảm {FormatPercent(promotion.DiscountValue)}%, tối đa {FormatMoney(promotion.MaximumDiscountValue)}đ",
            DiscountType.Percentage =>
                $"Giảm {FormatPercent(promotion.DiscountValue)}%",
            DiscountType.Fixed =>
                $"Giảm {FormatMoney(promotion.DiscountValue)}đ",
            _ => "Ưu đãi SafeRide"
        };
    }

    private static string FormatPercent(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}

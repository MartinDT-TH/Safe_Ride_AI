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

    public GetAvailablePromotionsQueryHandler(
        IPromotionRepository promotionRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _promotionRepository = promotionRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyList<AvailablePromotionResponse>> Handle(
        GetAvailablePromotionsQuery request,
        CancellationToken cancellationToken)
    {
        var promotions = await _promotionRepository.GetAvailablePromotionsAsync(
            _dateTimeProvider.UtcNow,
            cancellationToken);

        var responses = new List<AvailablePromotionResponse>();
        foreach (var promotion in promotions)
        {
            var usageCount = await _promotionRepository.CountCustomerPromotionUsageAsync(
                request.CustomerId,
                promotion.Id,
                cancellationToken);

            if (usageCount >= promotion.UsageLimitPerUser)
            {
                continue;
            }

            responses.Add(ToResponse(promotion));
        }

        return responses;
    }

    private static AvailablePromotionResponse ToResponse(Promotion promotion)
    {
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
            CreateShortDescription(promotion));
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

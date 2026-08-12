using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Promotions;

namespace SafeRide.Application.Features.Promotions.Queries.GetAdminPromotions;

public sealed class GetAdminPromotionsQueryHandler
    : IRequestHandler<GetAdminPromotionsQuery, AdminPromotionsPageResponse>
{
    private static readonly HashSet<string> SupportedStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "all",
            "active",
            "inactive",
            "expired",
            "upcoming"
        };

    private readonly IAdminPromotionRepository _promotionRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPromotionUnlockRuleStore _unlockRuleStore;

    public GetAdminPromotionsQueryHandler(
        IAdminPromotionRepository promotionRepository,
        IDateTimeProvider dateTimeProvider,
        IPromotionUnlockRuleStore unlockRuleStore)
    {
        _promotionRepository = promotionRepository;
        _dateTimeProvider = dateTimeProvider;
        _unlockRuleStore = unlockRuleStore;
    }

    public async Task<AdminPromotionsPageResponse> Handle(
        GetAdminPromotionsQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = request.Search?.Trim();
        var status = string.IsNullOrWhiteSpace(request.Status)
            ? "all"
            : request.Status.Trim().ToLowerInvariant();

        if (!SupportedStatuses.Contains(status))
        {
            throw new PromotionException(
                "admin_promotion.invalid_status",
                "Trạng thái khuyến mãi không hợp lệ.",
                400);
        }

        var data = await _promotionRepository.GetAdminPromotionsAsync(
            page,
            pageSize,
            search,
            status,
            _dateTimeProvider.UtcNow,
            cancellationToken);

        var requiredTripsByCode = await _unlockRuleStore.GetRequiredCompletedTripsAsync(
            data.Items.Select(item => item.PromotionCode).ToList(),
            cancellationToken);

        return new AdminPromotionsPageResponse(
            data.Items.Select(item => AdminPromotionRules.ToResponse(
                item,
                requiredTripsByCode.GetValueOrDefault(item.PromotionCode))).ToList(),
            new AdminPromotionCountsResponse(
                data.Total,
                data.Active,
                data.Inactive,
                data.Expired),
            page,
            pageSize,
            data.TotalItems,
            Math.Max(1, (int)Math.Ceiling(data.TotalItems / (double)pageSize)));
    }
}

using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Promotions;

namespace SafeRide.Application.Features.Promotions.Commands.UpdateAdminPromotion;

public sealed class UpdateAdminPromotionCommandHandler
    : IRequestHandler<UpdateAdminPromotionCommand, AdminPromotionResponse>
{
    private readonly IAdminPromotionRepository _promotionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPromotionUnlockRuleStore _unlockRuleStore;

    public UpdateAdminPromotionCommandHandler(
        IAdminPromotionRepository promotionRepository,
        IUnitOfWork unitOfWork,
        IPromotionUnlockRuleStore unlockRuleStore)
    {
        _promotionRepository = promotionRepository;
        _unitOfWork = unitOfWork;
        _unlockRuleStore = unlockRuleStore;
    }

    public async Task<AdminPromotionResponse> Handle(
        UpdateAdminPromotionCommand request,
        CancellationToken cancellationToken)
    {
        var promotionCode = AdminPromotionRules.NormalizeCode(request.PromotionCode);
        AdminPromotionRules.Validate(
            request.DiscountType,
            request.DiscountValue,
            request.StartDate,
            request.EndDate,
            request.MaxUsageCount,
            request.MinimumOrderValue,
            request.MaximumDiscountValue,
            request.UsageLimitPerUser,
            request.RequiredCompletedTrips);

        var promotion = await _promotionRepository.GetByIdAsync(
            request.PromotionId,
            cancellationToken);
        if (promotion is null)
        {
            throw new PromotionException(
                "admin_promotion.not_found",
                "Không tìm thấy khuyến mãi.",
                404);
        }

        AdminPromotionRules.ValidateMaxUsageForUpdate(
            request.MaxUsageCount,
            promotion.CurrentUsageCount);

        if (await _promotionRepository.CodeExistsAsync(
                promotionCode,
                promotion.Id,
                cancellationToken))
        {
            throw new PromotionException(
                "admin_promotion.code_conflict",
                "Mã khuyến mãi đã tồn tại.",
                409);
        }

        var oldPromotionCode = promotion.PromotionCode;
        promotion.PromotionCode = promotionCode;
        promotion.DiscountType = request.DiscountType;
        promotion.DiscountValue = request.DiscountValue;
        promotion.StartDate = request.StartDate;
        promotion.EndDate = request.EndDate;
        promotion.MaxUsageCount = request.MaxUsageCount;
        promotion.MinimumOrderValue = request.MinimumOrderValue;
        promotion.MaximumDiscountValue = request.MaximumDiscountValue;
        promotion.UsageLimitPerUser = request.UsageLimitPerUser;
        promotion.IsActive = request.IsActive;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requiredCompletedTrips = request.RequiredCompletedTrips ?? 0;
        if (!string.Equals(oldPromotionCode, promotionCode, StringComparison.Ordinal))
        {
            await _unlockRuleStore.RemoveAsync(oldPromotionCode, cancellationToken);
        }

        if (requiredCompletedTrips > 0)
        {
            await _unlockRuleStore.SaveAsync(
                promotionCode,
                requiredCompletedTrips,
                cancellationToken);
        }
        else
        {
            await _unlockRuleStore.RemoveAsync(promotionCode, cancellationToken);
        }

        return AdminPromotionRules.ToResponse(promotion, requiredCompletedTrips);
    }
}

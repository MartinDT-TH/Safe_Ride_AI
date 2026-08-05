using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Promotions;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.Promotions.Commands.CreateAdminPromotion;

public sealed class CreateAdminPromotionCommandHandler
    : IRequestHandler<CreateAdminPromotionCommand, AdminPromotionResponse>
{
    private readonly IAdminPromotionRepository _promotionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAdminPromotionCommandHandler(
        IAdminPromotionRepository promotionRepository,
        IUnitOfWork unitOfWork)
    {
        _promotionRepository = promotionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminPromotionResponse> Handle(
        CreateAdminPromotionCommand request,
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
            request.UsageLimitPerUser);

        if (await _promotionRepository.CodeExistsAsync(
                promotionCode,
                null,
                cancellationToken))
        {
            throw DuplicateCodeException();
        }

        var promotion = new Promotion
        {
            PromotionCode = promotionCode,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaxUsageCount = request.MaxUsageCount,
            CurrentUsageCount = 0,
            MinimumOrderValue = request.MinimumOrderValue,
            MaximumDiscountValue = request.MaximumDiscountValue,
            UsageLimitPerUser = request.UsageLimitPerUser,
            IsActive = request.IsActive
        };

        await _promotionRepository.AddAsync(promotion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return AdminPromotionRules.ToResponse(promotion);
    }

    private static PromotionException DuplicateCodeException()
    {
        return new PromotionException(
            "admin_promotion.code_conflict",
            "Mã khuyến mãi đã tồn tại.",
            409);
    }
}

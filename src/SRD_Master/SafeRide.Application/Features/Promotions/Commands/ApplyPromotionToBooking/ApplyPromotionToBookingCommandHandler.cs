using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Promotions;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Promotions.Commands.ApplyPromotionToBooking;

public sealed class ApplyPromotionToBookingCommandHandler
    : IRequestHandler<ApplyPromotionToBookingCommand, ApplyPromotionToBookingResponse>
{
    private readonly IPromotionRepository _promotionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ApplyPromotionToBookingCommandHandler(
        IPromotionRepository promotionRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _promotionRepository = promotionRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ApplyPromotionToBookingResponse> Handle(
        ApplyPromotionToBookingCommand request,
        CancellationToken cancellationToken)
    {
        var promotionCode = PromotionApplicationRules.NormalizePromotionCode(
            request.PromotionCode);
        var promotion = await GetPromotionAsync(promotionCode, cancellationToken);
        var utcNow = _dateTimeProvider.UtcNow;

        PromotionApplicationRules.ValidateAvailability(promotion, utcNow);

        var booking = await _promotionRepository.GetBookingForPromotionAsync(
            request.BookingId,
            cancellationToken);
        if (booking is null)
        {
            throw new PromotionException(
                "promotion.booking_not_found",
                "Không tìm thấy booking.",
                404);
        }

        ValidateBooking(booking, request.CustomerId);
        PromotionApplicationRules.ValidateMinimumOrderValue(
            booking.EstimatedFare,
            promotion.MinimumOrderValue);
        await ValidateCustomerUsageAsync(
            request.CustomerId,
            promotion.Id,
            promotion.UsageLimitPerUser,
            cancellationToken);

        var originalFare = booking.EstimatedFare;
        var discountAmount = PromotionApplicationRules.CalculateDiscountAmount(
            promotion,
            originalFare);
        var finalFare = Math.Max(0m, originalFare - discountAmount);

        await _promotionRepository.AddBookingPromotionAsync(
            new BookingPromotion
            {
                BookingId = booking.BookingId,
                PromotionId = promotion.Id,
                DiscountAmount = discountAmount,
                CreatedAt = utcNow
            },
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApplyPromotionToBookingResponse(
            booking.BookingId,
            promotion.PromotionCode,
            originalFare,
            discountAmount,
            finalFare,
            "Áp dụng mã khuyến mãi thành công.");
    }

    private async Task<Promotion> GetPromotionAsync(
        string promotionCode,
        CancellationToken cancellationToken)
    {
        var promotion = await _promotionRepository.GetPromotionByCodeAsync(
            promotionCode,
            cancellationToken);
        if (promotion is not null)
        {
            return promotion;
        }

        throw new PromotionException(
            "promotion.not_found",
            "Mã khuyến mãi không tồn tại.",
            404);
    }

    private static void ValidateBooking(
        Booking booking,
        Guid customerId)
    {
        if (booking.CustomerId != customerId)
        {
            throw new PromotionException(
                "promotion.booking_forbidden",
                "Bạn không có quyền áp dụng mã cho booking này.",
                403);
        }

        if (booking.BookingStatus is BookingStatus.Cancelled
            or BookingStatus.Completed
            or BookingStatus.Expired)
        {
            throw new PromotionException(
                "promotion.booking_not_applicable",
                "Không thể áp dụng mã khuyến mãi cho booking này.",
                400);
        }

        if (booking.BookingPromotions.Any())
        {
            throw new PromotionException(
                "promotion.booking_already_applied",
                "Booking này đã được áp dụng mã khuyến mãi.",
                409);
        }
    }

    private async Task ValidateCustomerUsageAsync(
        Guid customerId,
        long promotionId,
        int usageLimitPerUser,
        CancellationToken cancellationToken)
    {
        var usageCount = await _promotionRepository.CountCustomerPromotionUsageAsync(
            customerId,
            promotionId,
            cancellationToken);
        PromotionApplicationRules.ValidateCustomerUsageLimit(
            usageCount,
            usageLimitPerUser);
    }
}

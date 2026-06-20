using SafeRide.Domain.Entities;

namespace SafeRide.Application.Common.Interfaces;

public interface IPromotionRepository
{
    Task<IReadOnlyList<Promotion>> GetAvailablePromotionsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<Promotion?> GetPromotionByCodeAsync(
        string promotionCode,
        CancellationToken cancellationToken);

    Task<Booking?> GetBookingForPromotionAsync(
        long bookingId,
        CancellationToken cancellationToken);

    Task<int> CountCustomerPromotionUsageAsync(
        Guid customerId,
        long promotionId,
        CancellationToken cancellationToken);

    Task AddBookingPromotionAsync(
        BookingPromotion bookingPromotion,
        CancellationToken cancellationToken);

    Task RemoveBookingPromotionsForBookingAsync(
        long bookingId,
        CancellationToken cancellationToken);
}

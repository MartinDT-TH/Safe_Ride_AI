using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.Bookings.DTOs;

public sealed record BookingPriceDto(
    decimal OriginalFare,
    string? PromotionCode,
    decimal DiscountAmount,
    decimal FinalFare);

public static class BookingPriceMapper
{
    public static BookingPriceDto FromBooking(Booking booking)
    {
        var originalFare = booking.EstimatedFare;
        var bookingPromotion = booking.BookingPromotions.FirstOrDefault();
        var discountAmount = bookingPromotion?.DiscountAmount ?? 0m;

        return new BookingPriceDto(
            originalFare,
            bookingPromotion?.Promotion?.PromotionCode,
            discountAmount,
            Math.Max(0m, originalFare - discountAmount));
    }
}

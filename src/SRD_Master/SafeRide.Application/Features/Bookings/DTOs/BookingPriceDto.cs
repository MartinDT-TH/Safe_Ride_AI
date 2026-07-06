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
        var originalFare = booking.Trip?.ActualFare ?? booking.EstimatedFare;
        var bookingPromotion = booking.BookingPromotions.FirstOrDefault();
        var discountAmount = bookingPromotion?.DiscountAmount ?? 0m;

        var finalFare = booking.Trip?.FinalFare ?? Math.Max(0m, originalFare - discountAmount);

        return new BookingPriceDto(
            originalFare,
            bookingPromotion?.Promotion?.PromotionCode,
            discountAmount,
            finalFare);
    }
}

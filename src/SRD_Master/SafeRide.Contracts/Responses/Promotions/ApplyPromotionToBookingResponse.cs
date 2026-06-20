namespace SafeRide.Contracts.Responses.Promotions;

public sealed record ApplyPromotionToBookingResponse(
    long BookingId,
    string PromotionCode,
    decimal OriginalFare,
    decimal DiscountAmount,
    decimal FinalFare,
    string Message);

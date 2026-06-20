namespace SafeRide.Contracts.Requests.Bookings;

public sealed class ApplyPromotionToBookingRequest
{
    public string PromotionCode { get; set; } = string.Empty;
}

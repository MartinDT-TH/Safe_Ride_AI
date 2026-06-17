namespace SafeRide.Contracts.Requests.Bookings;

public sealed class CancelBookingRequest
{
    public string? Reason { get; set; }
}
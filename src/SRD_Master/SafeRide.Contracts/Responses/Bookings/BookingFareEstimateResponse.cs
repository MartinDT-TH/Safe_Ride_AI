namespace SafeRide.Contracts.Responses.Bookings;

public sealed record BookingFareEstimateResponse(
    double EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    string? EncodedPolyline,
    decimal EstimatedFare);

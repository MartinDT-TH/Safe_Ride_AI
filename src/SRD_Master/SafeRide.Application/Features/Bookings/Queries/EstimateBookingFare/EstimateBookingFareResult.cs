namespace SafeRide.Application.Features.Bookings.Queries.EstimateBookingFare;

public sealed record EstimateBookingFareResult(
    double EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    string? EncodedPolyline,
    decimal EstimatedFare,
    decimal? SurgeMultiplier);

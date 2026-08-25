namespace SafeRide.Application.Features.Bookings.Queries.EstimateBookingFare;

public sealed record EstimateBookingFareResult(
    double EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    string? EncodedPolyline,
    decimal EstimatedFare,
    decimal NormalFare,
    decimal SurgedFare,
    decimal SurgeAmount,
    decimal LongDistanceComponent,
    decimal MinimumServiceFare,
    decimal SurgeMultiplier);

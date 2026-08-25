namespace SafeRide.Application.Common.Models;

public sealed record BookingFareBreakdown(
    decimal NormalFare,
    decimal SurgedFare,
    decimal SurgeAmount,
    decimal LongDistanceComponent,
    decimal EstimatedFare);

namespace SafeRide.Application.Common.Models;

public sealed record CustomerNoShowReportResponse(
    long EventId,
    long TripId,
    long BookingId,
    decimal CustomerCharge,
    bool? DriverSupportEligible,
    string Message);

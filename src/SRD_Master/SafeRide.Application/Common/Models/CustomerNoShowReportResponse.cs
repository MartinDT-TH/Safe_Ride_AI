namespace SafeRide.Application.Common.Models;

using SafeRide.Domain.Enums;

public sealed record CustomerNoShowReportResponse(
    long EventId,
    long TripId,
    long BookingId,
    decimal CustomerCharge,
    bool? DriverSupportEligible,
    decimal? DriverSupportAmount,
    DriverNoShowSupportStatus? DriverSupportStatus,
    string Message);

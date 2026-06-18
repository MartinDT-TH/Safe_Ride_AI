using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Responses.Bookings;

public sealed record BookingDriverOfferResponse(
    long OfferId,
    Guid DriverId,
    string DriverName,
    string? DriverAvatarUrl,
    double Rating,
    int TripCount,
    int ExperienceYears,
    LicenseClass LicenseClass,
    DateTime ExpiresAt);

public sealed record BookingLocationResponse(
    string Address,
    double Latitude,
    double Longitude);

public sealed record BookingVehicleSummaryResponse(
    long Id,
    string Name,
    string PlateNumber,
    string Color,
    bool IsMotorbike);

public sealed record BookingResponse(
    long BookingId,
    BookingType BookingType,
    BookingStatus BookingStatus,
    DateTime? ScheduledAt,
    double EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    decimal EstimatedFare,
    string? EncodedPolyline,
    string Message,
    BookingDriverOfferResponse? DriverOffer = null,
    BookingLocationResponse? Pickup = null,
    BookingLocationResponse? Destination = null,
    BookingVehicleSummaryResponse? Vehicle = null,
    TripStatus? TripStatus = null,
    long? TripId = null,
    string? ArrivalPolyline = null);

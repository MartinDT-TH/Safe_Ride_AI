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
    BookingDriverOfferResponse? DriverOffer = null);

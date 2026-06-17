using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Redis;

public sealed record DriverLocationCache(
    Guid DriverId,
    double Latitude,
    double Longitude,
    DateTime UpdatedAt);

public sealed record MatchingBookingCache(
    long BookingId,
    Guid CustomerId,
    long VehicleId,
    RequiredLicenseClass RequiredLicenseClass,
    double PickupLatitude,
    double PickupLongitude,
    DateTime StartedAt);

public sealed record MatchingOfferCache(
    long BookingId,
    long OfferId,
    Guid DriverId,
    DateTime OfferedAt,
    DateTime ExpiresAt);

public sealed record TripLiveCache(
    long TripId,
    long BookingId,
    Guid DriverId,
    Guid CustomerId,
    TripStatus TripStatus,
    DateTime DriverAssignedAt);

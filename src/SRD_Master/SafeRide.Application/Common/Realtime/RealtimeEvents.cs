using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Realtime;

public sealed record BookingStatusChangedEvent(
    long BookingId,
    Guid CustomerId,
    BookingStatus BookingStatus,
    DateTime UpdatedAt);

public sealed record TripStatusChangedEvent(
    long TripId,
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    TripStatus TripStatus,
    DateTime UpdatedAt);

public sealed record TripCreatedEvent(
    long TripId,
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    TripStatus TripStatus,
    DateTime DriverAssignedAt);

public sealed record DriverLocationUpdatedEvent(
    Guid DriverId,
    Guid? CustomerId,
    long? TripId,
    double Latitude,
    double Longitude,
    DateTime UpdatedAt);

public sealed record DriverOfferCreatedEvent(
    long BookingId,
    Guid CustomerId,
    BookingDriverOfferDto DriverOffer);

public sealed record DriverOfferRejectedEvent(
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    long OfferId,
    DateTime RejectedAt);

public sealed record DriverMatchedEvent(
    long BookingId,
    Guid DriverId,
    DateTime OfferedAt,
    DateTime ExpiresAt);

using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Realtime;

public sealed record BookingStatusChangedEvent(
    long BookingId,
    Guid CustomerId,
    BookingStatus BookingStatus,
    DateTime UpdatedAt);

public sealed record BookingSearchingStartedEvent(
    long BookingId,
    Guid CustomerId,
    double RadiusKm,
    DateTime StartedAt,
    string Message);

public sealed record TripStatusChangedEvent(
    long TripId,
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    TripStatus TripStatus,
    DateTime UpdatedAt,
    BookingStatus? BookingStatus = null);

public sealed record TripCreatedEvent(
    long TripId,
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    TripStatus TripStatus,
    DateTime DriverAssignedAt,
    BookingStatus BookingStatus = BookingStatus.DriverAssigned);

public sealed record BookingDriverAssignedEvent(
    long BookingId,
    long TripId,
    Guid CustomerId,
    Guid DriverId,
    DateTime AssignedAt,
    string Message,
    BookingDriverOfferDto? DriverOffer = null,
    BookingStatus BookingStatus = BookingStatus.DriverAssigned,
    TripStatus TripStatus = TripStatus.ACCEPTED);

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

public sealed record DriverOfferReceivedEvent(
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    BookingDriverOfferDto DriverOffer,
    string Message);

public sealed record DriverOfferRejectedEvent(
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    long OfferId,
    DateTime RejectedAt);

public sealed record DriverOfferAcceptedEvent(
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    long OfferId,
    DateTime AcceptedAt,
    DateTime CustomerConfirmExpiresAt,
    BookingDriverOfferDto DriverOffer,
    string Message,
    BookingStatus BookingStatus = BookingStatus.Searching,
    string? MatchingMessage = null);

public sealed record DriverOfferExpiredEvent(
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    long OfferId,
    DateTime ExpiredAt,
    string Message);

public sealed record DriverOfferCancelledEvent(
    long BookingId,
    Guid CustomerId,
    Guid DriverId,
    long OfferId,
    DateTime CancelledAt,
    string Message);

public sealed record CustomerConfirmedDriverOfferEvent(
    long BookingId,
    long TripId,
    Guid CustomerId,
    Guid DriverId,
    long OfferId,
    DateTime ConfirmedAt,
    string Message);

public sealed record DriverMatchedEvent(
    long BookingId,
    Guid DriverId,
    DateTime OfferedAt,
    DateTime ExpiresAt);

public sealed record BookingSearchRadiusExpandedEvent(
    long BookingId,
    Guid CustomerId,
    double PreviousRadiusKm,
    double CurrentRadiusKm,
    DateTime ExpandedAt,
    string Message);

public sealed record BookingExpiredEvent(
    long BookingId,
    Guid CustomerId,
    DateTime ExpiredAt,
    string Message);

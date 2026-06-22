using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.DTOs;

public sealed record BookingLocationDto(
    string Address,
    double Latitude,
    double Longitude);

public sealed record BookingVehicleSummaryDto(
    long Id,
    string Name,
    string PlateNumber,
    string Color,
    bool IsMotorbike);

public sealed record BookingDetailsDto(
    long BookingId,
    BookingType BookingType,
    BookingStatus BookingStatus,
    DateTime? ScheduledAt,
    double EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    decimal EstimatedFare,
    decimal OriginalFare,
    string? PromotionCode,
    decimal DiscountAmount,
    decimal FinalFare,
    string? EncodedPolyline,
    string? ArrivalPolyline,
    string Message,
    BookingDriverOfferDto? DriverOffer,
    BookingLocationDto Pickup,
    BookingLocationDto? Destination,
    BookingVehicleSummaryDto Vehicle,
    long? TripId,
    TripStatus? TripStatus,
    double? CurrentSearchRadiusKm = null,
    DateTime? ExpiresAt = null,
    int? EstimatedRemainingSeconds = null,
    string? MatchingMessage = null);

using SafeRide.Domain.Enums;
using SafeRide.Application.Features.Bookings.DTOs;

namespace SafeRide.Application.Features.Bookings.Commands.CreateBooking;

public sealed record CreateBookingResponse(
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
    string Message,
    long? TripId = null,
    BookingDriverOfferDto? DriverOffer = null,
    TripStatus? TripStatus = null,
    double? CurrentSearchRadiusKm = null,
    DateTime? ExpiresAt = null,
    int? EstimatedRemainingSeconds = null,
    string? MatchingMessage = null);

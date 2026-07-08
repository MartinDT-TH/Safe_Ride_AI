using SafeRide.Application.Features.Trips.DTOs;
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

public sealed record TripPaymentSummaryDto(
    long? PaymentId,
    PaymentMethod? PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal Amount,
    string Currency,
    DateTime? PaidAt,
    string Message);

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
    TripReturnConfirmationSummaryDto? ReturnConfirmation,
    double? CurrentSearchRadiusKm = null,
    DateTime? ExpiresAt = null,
    int? EstimatedRemainingSeconds = null,
    string? MatchingMessage = null,
    TripPaymentSummaryDto? Payment = null,
    double? ActualDistanceKm = null,
    int? ActualDurationMinutes = null,
    string? ActualEncodedPolyline = null,
    DateTime? TripEndedAt = null);

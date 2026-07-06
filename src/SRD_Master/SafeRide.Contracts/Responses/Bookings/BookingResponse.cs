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
    DateTime ExpiresAt,
    DriverOfferStatus OfferStatus,
    double? DriverLatitude = null,
    double? DriverLongitude = null,
    int? CustomerConfirmRemainingSeconds = null);

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

public sealed record TripReturnEvidenceSummaryResponse(
    long Id,
    string ImageUrl,
    string? ContentType,
    int DisplayOrder);

public sealed record TripReturnConfirmationSummaryResponse(
    long Id,
    HandoverStatus HandoverStatus,
    Guid DriverId,
    Guid ConfirmedByUserId,
    DateTime ConfirmedAt,
    decimal? DriverLatitude,
    decimal? DriverLongitude,
    string? Note,
    IReadOnlyList<TripReturnEvidenceSummaryResponse> Evidence);

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
    string? ArrivalPolyline = null,
    decimal OriginalFare = 0m,
    string? PromotionCode = null,
    decimal DiscountAmount = 0m,
    decimal FinalFare = 0m,
    double? CurrentSearchRadiusKm = null,
    DateTime? ExpiresAt = null,
    int? EstimatedRemainingSeconds = null,
    string? MatchingMessage = null,
    TripReturnConfirmationSummaryResponse? ReturnConfirmation = null);

using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminTrips;

public sealed record AdminTripDetailsResponse(
    long TripId,
    string TripCode,
    long BookingId,
    string BookingCode,
    TripStatus TripStatus,
    BookingStatus BookingStatus,
    BookingType BookingType,
    string ServiceName,
    AdminTripUserResponse Customer,
    AdminTripDriverResponse Driver,
    AdminTripVehicleResponse Vehicle,
    AdminTripLocationResponse PickupLocation,
    AdminTripLocationResponse? DestinationLocation,
    AdminTripRouteResponse Route,
    AdminTripTimelineResponse Timeline,
    AdminTripFareResponse Fare,
    AdminTripPaymentResponse? Payment,
    IReadOnlyList<AdminTripPromotionResponse> Promotions,
    string? TripNotes,
    AdminTripRatingResponse? Rating,
    DateTime CreatedAt,
    DateTime LastUpdatedAt);

public sealed record AdminTripUserResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? AvatarUrl);

public sealed record AdminTripDriverResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? AvatarUrl,
    DriverWorkStatus WorkStatus,
    int? ExperienceYears,
    double? AverageRating);

public sealed record AdminTripVehicleResponse(
    long Id,
    string BrandModel,
    string PlateNumber,
    string? Color,
    VehicleType VehicleType,
    EngineType EngineType,
    TransmissionType TransmissionType,
    int? EngineCapacityCc,
    RequiredLicenseClass RequiredLicenseClass);

public sealed record AdminTripLocationResponse(
    string? Address,
    double? Latitude,
    double? Longitude);

public sealed record AdminTripRouteResponse(
    decimal? EstimatedDistanceKm,
    decimal? ActualDistanceKm,
    int? EstimatedDurationMinutes,
    int? ActualDurationMinutes,
    string? RoutePolyline,
    bool IsSosActivated,
    int RouteDeviationCount,
    int SosAlertCount);

public sealed record AdminTripTimelineResponse(
    DateTime BookingCreatedAt,
    DateTime? ScheduledAt,
    DateTime? DriverAssignedAt,
    DateTime? ArrivedAt,
    DateTime? StartedAt,
    DateTime? EndedAt,
    DateTime? CompletedAt);

public sealed record AdminTripFareResponse(
    decimal EstimatedFare,
    decimal? ActualFare,
    decimal FinalFare,
    decimal DiscountAmount);

public sealed record AdminTripPaymentResponse(
    long Id,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal Amount,
    string Currency,
    DateTime? PaidAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AdminTripPromotionResponse(
    long Id,
    string PromotionCode,
    DiscountType? DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount);

public sealed record AdminTripRatingResponse(
    int RatingScore,
    string? Comment,
    DateTime CreatedAt);

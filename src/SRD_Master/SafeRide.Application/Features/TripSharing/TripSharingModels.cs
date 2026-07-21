namespace SafeRide.Application.Features.TripSharing;

public sealed record TripShareRecipientDto(
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    string MaskedPhoneNumber);

public sealed record CreateTripShareResult(
    long TripShareId,
    TripShareRecipientDto Recipient,
    string ShareUrl,
    DateTime ExpiresAt);

public sealed record TripShareListItemDto(
    long TripShareId,
    TripShareRecipientDto Recipient,
    DateTime? OpenedAt,
    DateTime ExpiresAt,
    DateTime? RevokedAt,
    bool IsActive);

public sealed record TripShareSharedByDto(
    string FullName,
    string? AvatarUrl);

public sealed record ReceivedTripShareListItemDto(
    long TripShareId,
    string TripStatus,
    TripShareSharedByDto SharedBy,
    DateTime? OpenedAt,
    DateTime ExpiresAt,
    bool IsActive);

public sealed record ResolveTripShareResult(
    long TripShareId,
    long TripId,
    string TripStatus);

public sealed record SharedTripPointDto(
    double Latitude,
    double Longitude,
    string? Address = null);

public sealed record SharedTripDriverDto(
    string FullName,
    string? AvatarUrl,
    double? Rating);

public sealed record SharedTripVehicleDto(
    string BrandModel,
    string? Color,
    string MaskedPlateNumber);

public sealed record SharedTripTrackingDto(
    long TripShareId,
    string TripStatus,
    SharedTripPointDto Pickup,
    SharedTripPointDto? Destination,
    SharedTripPointDto? CurrentDriverLocation,
    DateTime? LastLocationUpdate,
    string? RoutePolyline,
    SharedTripDriverDto Driver,
    SharedTripVehicleDto Vehicle,
    DateTime? EstimatedArrival);

public sealed record SharedTripLocationUpdate(
    long TripShareId,
    double Latitude,
    double Longitude,
    DateTime UpdatedAt);

public sealed record SharedTripStatusUpdate(
    long TripShareId,
    string TripStatus,
    DateTime OccurredAt);

using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.DTOs;

public sealed record BookingDriverOfferDto(
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
    int? CustomerConfirmRemainingSeconds = null);

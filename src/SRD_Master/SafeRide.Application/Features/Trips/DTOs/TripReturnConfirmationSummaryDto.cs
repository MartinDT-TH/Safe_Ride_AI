using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Trips.DTOs;

public sealed record TripReturnEvidenceSummaryDto(
    long Id,
    string ImageUrl,
    string? ContentType,
    int DisplayOrder);

public sealed record TripReturnConfirmationSummaryDto(
    long Id,
    HandoverStatus HandoverStatus,
    Guid DriverId,
    Guid ConfirmedByUserId,
    DateTime ConfirmedAt,
    decimal? DriverLatitude,
    decimal? DriverLongitude,
    string? Note,
    IReadOnlyList<TripReturnEvidenceSummaryDto> Evidence);

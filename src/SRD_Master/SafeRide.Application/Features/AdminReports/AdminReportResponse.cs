namespace SafeRide.Application.Features.AdminReports;

public sealed record AdminReportResponse(
    long Id,
    long? TripId,
    long? BookingId,
    Guid ReporterUserId,
    string ReporterName,
    string? ReporterEmail,
    string? ReporterPhone,
    Guid? DriverId,
    string? DriverName,
    string? DriverEmail,
    string? DriverPhoneNumber,
    string Subject,
    string Description,
    string Status,
    DateTime CreatedAt);

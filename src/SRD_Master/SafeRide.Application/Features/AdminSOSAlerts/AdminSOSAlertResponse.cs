using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminSOSAlerts;

public sealed record AdminSOSAlertResponse(
    long SosAlertId,
    long TripId,
    long BookingId,
    Guid CustomerId,
    string? CustomerName,
    string? CustomerPhoneNumber,
    Guid? DriverId,
    string? DriverName,
    string? DriverPhoneNumber,
    double Latitude,
    double Longitude,
    string? Message,
    SOSStatus Status,
    DateTime CreatedAt);

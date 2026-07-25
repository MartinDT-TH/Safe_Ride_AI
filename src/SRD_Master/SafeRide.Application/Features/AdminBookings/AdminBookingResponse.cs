using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminBookings;

public sealed record AdminBookingResponse(
    long Id,
    string BookingCode,
    Guid CustomerId,
    string CustomerName,
    string? CustomerPhone,
    string? CustomerAvatarUrl,
    Guid? DriverId,
    string? DriverName,
    string? DriverPhone,
    string? DriverAvatarUrl,
    string PickupAddress,
    string? DestinationAddress,
    string VehicleName,
    string? VehiclePlateNumber,
    string? VehicleColor,
    VehicleType VehicleType,
    string ServiceName,
    BookingType BookingType,
    BookingStatus BookingStatus,
    decimal EstimatedFare,
    PaymentMethod? PaymentMethod,
    PaymentStatus? PaymentStatus,
    DateTime? ScheduledAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

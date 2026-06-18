using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingDetails;

internal static class BookingDetailsMapper
{
    public static async Task<BookingDetailsDto> ToDtoAsync(
        Booking booking,
        IBookingRepository repository,
        CancellationToken cancellationToken)
    {
        var driverOffer = await repository.GetLatestBookingDriverOfferAsync(
            booking.BookingId,
            cancellationToken);

        return new BookingDetailsDto(
            booking.BookingId,
            booking.BookingType,
            booking.BookingStatus,
            booking.ScheduledAt,
            (double)(booking.EstimatedDistanceKm ?? 0m),
            booking.EstimatedDurationMinutes ?? 0,
            booking.EstimatedFare,
            booking.RoutePolyline,
            "Lấy thông tin chuyến đi thành công.",
            driverOffer,
            new BookingLocationDto(
                booking.PickupAddress,
                booking.PickupLocation.Y,
                booking.PickupLocation.X),
            booking.DestinationLocation is null
                ? null
                : new BookingLocationDto(
                    booking.DestinationAddress ?? string.Empty,
                    booking.DestinationLocation.Y,
                    booking.DestinationLocation.X),
            new BookingVehicleSummaryDto(
                booking.Vehicle.Id,
                booking.Vehicle.BrandModel,
                booking.Vehicle.PlateNumber,
                booking.Vehicle.Color ?? string.Empty,
                booking.Vehicle.VehicleType == VehicleType.Motorbike),
            booking.Trip?.TripStatus);
    }
}

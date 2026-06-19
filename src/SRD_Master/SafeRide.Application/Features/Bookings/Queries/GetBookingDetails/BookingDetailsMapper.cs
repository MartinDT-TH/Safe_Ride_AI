using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingDetails;

internal static class BookingDetailsMapper
{
    public static async Task<BookingDetailsDto> ToDtoAsync(
        Booking booking,
        IBookingRepository repository,
        IGoogleMapsService googleMapsService,
        CancellationToken cancellationToken)
    {
        var driverOffer = await repository.GetLatestBookingDriverOfferAsync(
            booking.BookingId,
            cancellationToken);
        var arrivalPolyline = await GetArrivalPolylineAsync(
            booking,
            driverOffer,
            repository,
            googleMapsService,
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
            arrivalPolyline,
            "Loaded booking details successfully.",
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
            booking.Trip?.Id,
            booking.Trip?.TripStatus);
    }

    private static async Task<string?> GetArrivalPolylineAsync(
        Booking booking,
        BookingDriverOfferDto? driverOffer,
        IBookingRepository repository,
        IGoogleMapsService googleMapsService,
        CancellationToken cancellationToken)
    {
        if (booking.BookingStatus != BookingStatus.DriverAssigned
            || booking.Trip is null
            || driverOffer is null
            || booking.Trip.TripStatus is TripStatus.IN_PROGRESS
                or TripStatus.COMPLETED
                or TripStatus.CANCELLED)
        {
            return null;
        }

        var driverLocation = await repository.GetDriverLocationAsync(
            driverOffer.DriverId,
            cancellationToken);
        if (driverLocation is null)
        {
            return null;
        }

        try
        {
            var route = await googleMapsService.GetRouteEstimateAsync(
                driverLocation,
                new LocationPoint(
                    booking.PickupLocation.Y,
                    booking.PickupLocation.X),
                cancellationToken);

            return route.EncodedPolyline;
        }
        catch
        {
            return null;
        }
    }
}

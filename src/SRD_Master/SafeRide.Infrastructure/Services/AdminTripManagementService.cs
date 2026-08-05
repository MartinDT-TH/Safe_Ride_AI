using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AdminTrips;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class AdminTripManagementService : IAdminTripManagementService
{
    private readonly ApplicationDbContext _db;

    public AdminTripManagementService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AdminTripDetailsResponse?> GetTripDetailsByTripIdAsync(
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await BuildTripDetailsQuery()
            .FirstOrDefaultAsync(item => item.Id == tripId, cancellationToken);

        return await MapTripAsync(trip, cancellationToken);
    }

    public async Task<AdminTripDetailsResponse?> GetTripDetailsByBookingIdAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        var trip = await BuildTripDetailsQuery()
            .FirstOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);

        return await MapTripAsync(trip, cancellationToken);
    }

    private IQueryable<Trip> BuildTripDetailsQuery()
    {
        return _db.Trips
            .AsNoTracking()
            .AsSplitQuery()
            .Include(trip => trip.Booking)
                .ThenInclude(booking => booking.Customer)
            .Include(trip => trip.Booking)
                .ThenInclude(booking => booking.Vehicle)
            .Include(trip => trip.Booking)
                .ThenInclude(booking => booking.ServiceType)
            .Include(trip => trip.Booking)
                .ThenInclude(booking => booking.BookingPromotions)
                    .ThenInclude(bookingPromotion => bookingPromotion.Promotion)
            .Include(trip => trip.Driver)
                .ThenInclude(driver => driver.Driver)
            .Include(trip => trip.Payments)
            .Include(trip => trip.Rating)
            .Include(trip => trip.RouteDeviations)
            .Include(trip => trip.SOSAlerts);
    }

    private async Task<AdminTripDetailsResponse?> MapTripAsync(
        Trip? trip,
        CancellationToken cancellationToken)
    {
        if (trip is null)
        {
            return null;
        }

        var driverAverageRating = await _db.Ratings
            .AsNoTracking()
            .Where(rating => rating.DriverId == trip.DriverId)
            .Select(rating => (double?)rating.RatingScore)
            .AverageAsync(cancellationToken);

        return MapTrip(trip, driverAverageRating);
    }

    private static AdminTripDetailsResponse MapTrip(
        Trip trip,
        double? driverAverageRating)
    {
        var booking = trip.Booking;
        var customer = booking.Customer;
        var driverProfile = trip.Driver;
        var driver = driverProfile.Driver;
        var vehicle = booking.Vehicle;
        var latestPayment = SelectDisplayPayment(trip.Payments);
        var promotions = booking.BookingPromotions
            .OrderBy(bookingPromotion => bookingPromotion.CreatedAt)
            .Select(bookingPromotion => new AdminTripPromotionResponse(
                bookingPromotion.PromotionId,
                bookingPromotion.Promotion.PromotionCode,
                bookingPromotion.Promotion.DiscountType,
                bookingPromotion.Promotion.DiscountValue,
                bookingPromotion.DiscountAmount))
            .ToList();
        var discountAmount = promotions.Sum(promotion => promotion.DiscountAmount);
        var grossFare = trip.ActualFare ?? booking.EstimatedFare;
        var finalFare = trip.FinalFare
            ?? latestPayment?.Amount
            ?? Math.Max(0m, grossFare - discountAmount);

        return new AdminTripDetailsResponse(
            trip.Id,
            $"SR-{trip.Id}",
            booking.BookingId,
            $"SR-{booking.BookingId}",
            trip.TripStatus,
            booking.BookingStatus,
            booking.BookingType,
            booking.ServiceType.ServiceName,
            new AdminTripUserResponse(
                customer.Id,
                customer.FullName ?? customer.UserName ?? "Khach hang",
                customer.PhoneNumber,
                customer.Email,
                customer.AvatarUrl),
            new AdminTripDriverResponse(
                driver.Id,
                driver.FullName ?? driver.UserName ?? "Tai xe SafeRide",
                driver.PhoneNumber,
                driver.Email,
                driver.AvatarUrl,
                driverProfile.WorkStatus,
                driverProfile.ExperienceYears,
                driverAverageRating),
            new AdminTripVehicleResponse(
                vehicle.Id,
                vehicle.BrandModel,
                vehicle.PlateNumber,
                vehicle.Color,
                vehicle.VehicleType,
                vehicle.EngineType,
                vehicle.TransmissionType,
                vehicle.EngineCapacityCc,
                vehicle.RequiredLicenseClass),
            BuildLocation(booking.PickupAddress, booking.PickupLocation)!,
            BuildLocation(booking.DestinationAddress, booking.DestinationLocation),
            new AdminTripRouteResponse(
                booking.EstimatedDistanceKm,
                trip.ActualDistanceKm,
                booking.EstimatedDurationMinutes,
                trip.ActualDurationMinutes,
                trip.RoutePolyline ?? booking.RoutePolyline,
                trip.IsSOSActivated,
                trip.RouteDeviations.Count,
                trip.SOSAlerts.Count),
            new AdminTripTimelineResponse(
                booking.CreatedAt,
                booking.ScheduledAt,
                trip.DriverAssignedAt,
                trip.ArrivedAt,
                trip.StartedAt,
                trip.EndedAt,
                trip.CompletedAt),
            new AdminTripFareResponse(
                booking.EstimatedFare,
                trip.ActualFare,
                finalFare,
                discountAmount),
            latestPayment is null
                ? null
                : new AdminTripPaymentResponse(
                    latestPayment.Id,
                    latestPayment.PaymentMethod,
                    latestPayment.PaymentStatus,
                    latestPayment.Amount,
                    latestPayment.Currency,
                    latestPayment.PaidAt,
                    latestPayment.CreatedAt,
                    latestPayment.UpdatedAt),
            promotions,
            booking.SpecialRequest ?? trip.CancellationReason,
            trip.Rating is null
                ? null
                : new AdminTripRatingResponse(
                    trip.Rating.RatingScore,
                    trip.Rating.Comment,
                    trip.Rating.CreatedAt),
            trip.CreatedAt,
            GetLastUpdatedAt(trip, latestPayment));
    }

    private static Payment? SelectDisplayPayment(IEnumerable<Payment> payments)
    {
        return payments
            .OrderByDescending(payment => payment.PaymentStatus == PaymentStatus.Success)
            .ThenByDescending(payment => payment.UpdatedAt ?? payment.CreatedAt)
            .FirstOrDefault();
    }

    private static AdminTripLocationResponse? BuildLocation(
        string? address,
        Point? point)
    {
        if (address is null && point is null)
        {
            return null;
        }

        return new AdminTripLocationResponse(
            address,
            point?.Y,
            point?.X);
    }

    private static DateTime GetLastUpdatedAt(
        Trip trip,
        Payment? latestPayment)
    {
        var latest = trip.Booking.UpdatedAt;
        var candidates = new DateTime?[]
        {
            trip.CreatedAt,
            trip.DriverAssignedAt,
            trip.ArrivedAt,
            trip.StartedAt,
            trip.EndedAt,
            trip.CompletedAt,
            latestPayment?.UpdatedAt,
            latestPayment?.PaidAt,
            latestPayment?.CreatedAt,
            trip.Rating?.CreatedAt
        };

        foreach (var candidate in candidates)
        {
            if (candidate.HasValue && candidate.Value > latest)
            {
                latest = candidate.Value;
            }
        }

        return latest;
    }
}

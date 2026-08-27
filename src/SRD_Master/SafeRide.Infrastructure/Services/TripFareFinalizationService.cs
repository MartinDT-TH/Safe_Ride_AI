using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using Microsoft.Extensions.Options;

namespace SafeRide.Infrastructure.Services;

public sealed class TripFareFinalizationService
{
    private readonly IFareEstimationService _fareEstimationService;

    public TripFareFinalizationService(
        IFareEstimationService fareEstimationService,
        IOptions<DriverCompensationOptions> compensationOptions)
    {
        _fareEstimationService = fareEstimationService;
        DestinationReachedThresholdMeters =
            compensationOptions.Value.DestinationReachedThresholdMeters;
    }

    public double DestinationReachedThresholdMeters { get; }

    public bool IsDestinationReached(double distanceMeters) =>
        distanceMeters <= DestinationReachedThresholdMeters;

    public bool IsDestinationReached(
        double endLatitude,
        double endLongitude,
        double destinationLatitude,
        double destinationLongitude) =>
        IsDestinationReached(CalculateHaversineDistanceMeters(
            endLatitude,
            endLongitude,
            destinationLatitude,
            destinationLongitude));

    public TripFareFinalizationResult Calculate(
        Trip trip,
        decimal actualDistanceKm,
        int actualDurationMinutes)
    {
        var pricingRule = trip.Booking.PricingRule;
        var isPerKilometerTrip = pricingRule?.PricePerKm is > 0m
            && !pricingRule.PricePerHour.HasValue;
        if (isPerKilometerTrip && actualDistanceKm <= 0m)
        {
            return new TripFareFinalizationResult(0m, 0m);
        }

        var actualFare = pricingRule is null
            ? trip.Booking.EstimatedFare
            : _fareEstimationService.CalculateFare(
                pricingRule,
                actualDistanceKm,
                actualDurationMinutes,
                trip.Booking.SurgePricingRule);

        actualFare = RoundVnd(actualFare);
        var discountAmount = trip.Booking.BookingPromotions.Sum(x => x.DiscountAmount);
        var finalFare = RoundVnd(Math.Max(0m, actualFare - discountAmount));

        return new TripFareFinalizationResult(actualFare, finalFare);
    }

    public TripFareFinalizationResult CalculateLockedFare(
        Trip trip,
        TripEndReason reason,
        decimal plannedRouteProgress,
        bool destinationReached)
    {
        if (trip.Booking.PricingSnapshotVersion is null or < Booking.CurrentPricingSnapshotVersion)
        {
            throw new BookingException(
                "trip.pricing_snapshot_required",
                "Chuyến đi chưa có dữ liệu giá khóa để áp dụng cách tính mới.",
                409);
        }

        var lockedFare = RoundVnd(trip.Booking.EstimatedFare);
        if (!trip.Booking.AcceptedMinimumServiceFare.HasValue)
        {
            throw PricingSnapshotIncomplete();
        }

        var isHourlyBooking = trip.Booking.AcceptedPricePerHour is > 0m
            && !trip.Booking.AcceptedPricePerKm.HasValue;
        plannedRouteProgress = Math.Clamp(plannedRouteProgress, 0m, 1m);
        var actualFare = reason switch
        {
            TripEndReason.NORMAL_COMPLETION when destinationReached || isHourlyBooking => lockedFare,
            TripEndReason.NORMAL_COMPLETION => throw new BookingException(
                "trip.destination_not_reached",
                "Chưa xác nhận xe đã đến điểm đến đã đặt. Hãy chọn đúng lý do kết thúc chuyến.",
                409),
            TripEndReason.CUSTOMER_REQUESTED_STOP =>
                CalculateCustomerRequestedStopComponentAllocation(
                    trip.Booking,
                    plannedRouteProgress).GrossFare,
            TripEndReason.DRIVER_UNABLE_TO_CONTINUE => 0m,
            TripEndReason.STARTED_BY_MISTAKE => 0m,
            TripEndReason.SYSTEM_ERROR => throw new BookingException(
                "trip.system_error_reconciliation_required",
                "Lỗi hệ thống phải được xử lý qua quy trình đối soát có thẩm quyền.",
                409),
            TripEndReason.VEHICLE_SAFETY_ISSUE or
                TripEndReason.SAFETY_TERMINATION => throw new BookingException(
                    "trip.safety_termination_required",
                    "Hãy dùng quy trình kết thúc vì an toàn để Risk Protection xử lý chuyến đi.",
                    409),
            _ => throw new BookingException(
                "trip.invalid_end_reason",
                "Lý do kết thúc chuyến không hợp lệ.",
                400)
        };

        actualFare = RoundVnd(actualFare);
        var discountAmount = trip.Booking.BookingPromotions.Sum(x => x.DiscountAmount);
        var finalFare = RoundVnd(Math.Max(0m, actualFare - discountAmount));
        return new TripFareFinalizationResult(actualFare, finalFare);
    }

    public static CustomerRequestedStopComponentAllocation
        CalculateCustomerRequestedStopComponentAllocation(
            Booking booking,
            decimal plannedRouteProgress)
    {
        if (!booking.AcceptedMinimumServiceFare.HasValue
            || !booking.LongDistanceComponent.HasValue)
        {
            throw PricingSnapshotIncomplete();
        }

        var acceptedLongDistanceComponent = booking.LongDistanceComponent.Value;
        var acceptedFareComponent = booking.EstimatedFare - acceptedLongDistanceComponent;
        if (booking.EstimatedFare < 0m
            || booking.AcceptedMinimumServiceFare.Value < 0m
            || acceptedLongDistanceComponent < 0m
            || acceptedFareComponent < 0m)
        {
            throw PricingSnapshotIncomplete();
        }

        var progress = Math.Clamp(plannedRouteProgress, 0m, 1m);
        var progressGrossFare = RoundVnd(booking.EstimatedFare * progress);
        var progressLongDistanceComponent = RoundVnd(acceptedLongDistanceComponent * progress);
        var grossFare = Math.Max(
            progressGrossFare,
            RoundVnd(booking.AcceptedMinimumServiceFare.Value));
        var finalFareComponent = grossFare - progressLongDistanceComponent;

        return new CustomerRequestedStopComponentAllocation(
            grossFare,
            finalFareComponent,
            progressLongDistanceComponent);
    }

    private static decimal RoundVnd(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    private static BookingException PricingSnapshotIncomplete() => new(
        "trip.pricing_snapshot_incomplete",
        "Dữ liệu giá khóa của chuyến đi chưa đầy đủ.",
        409);

    private static double CalculateHaversineDistanceMeters(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusMeters = 6_371_000d;
        static double ToRadians(double degrees) => degrees * Math.PI / 180d;

        var latitudeDelta = ToRadians(latitude2 - latitude1);
        var longitudeDelta = ToRadians(longitude2 - longitude1);
        var firstLatitude = ToRadians(latitude1);
        var secondLatitude = ToRadians(latitude2);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2d), 2d)
            + Math.Cos(firstLatitude) * Math.Cos(secondLatitude)
            * Math.Pow(Math.Sin(longitudeDelta / 2d), 2d);
        return earthRadiusMeters * 2d * Math.Atan2(
            Math.Sqrt(haversine),
            Math.Sqrt(1d - haversine));
    }
}

public sealed record TripFareFinalizationResult(
    decimal ActualFare,
    decimal FinalFare);

public sealed record CustomerRequestedStopComponentAllocation(
    decimal GrossFare,
    decimal FareComponent,
    decimal LongDistanceComponent);

using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Services;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace SafeRide.UnitTests;

public sealed class FareEstimationServiceValidationTests
{
    [Fact]
    public void FinalizeFare_NormalCompletion_UsesLockedEstimatedFare()
    {
        var trip = CreateSnapshottedTrip();
        trip.Booking.BookingPromotions.Add(new BookingPromotion
        {
            DiscountAmount = 10_000m
        });
        var service = CreateService();

        var result = service.CalculateLockedFare(
            trip,
            TripEndReason.NORMAL_COMPLETION,
            plannedRouteProgress: 0.2m,
            destinationReached: true);

        Assert.Equal(120_000m, result.ActualFare);
        Assert.Equal(110_000m, result.FinalFare);
    }

    [Fact]
    public void FinalizeFare_CurrentPricingAndSurgeChanges_DoNotRepriceSnapshot()
    {
        var trip = CreateSnapshottedTrip();
        trip.Booking.PricingRule!.BaseFare = 1m;
        trip.Booking.PricingRule.PricePerKm = 1m;
        trip.Booking.SurgePricingRule!.SurgeMultiplier = 25m;

        var result = CreateService().CalculateLockedFare(
            trip,
            TripEndReason.NORMAL_COMPLETION,
            plannedRouteProgress: 1m,
            destinationReached: true);

        Assert.Equal(120_000m, result.ActualFare);
        Assert.Equal(120_000m, result.FinalFare);
    }

    [Theory]
    [InlineData(0, 30_000)]
    [InlineData(0.05, 30_000)]
    [InlineData(0.49, 58_800)]
    [InlineData(0.499999, 60_000)]
    [InlineData(0.5, 120_000)]
    [InlineData(0.75, 120_000)]
    [InlineData(1, 120_000)]
    public void FinalizeFare_CustomerRequestedStop_UsesInclusiveHalfProgressThreshold(
        double progress,
        decimal expectedFare)
    {
        var trip = CreateSnapshottedTrip();
        var service = CreateService();

        var result = service.CalculateLockedFare(
            trip,
            TripEndReason.CUSTOMER_REQUESTED_STOP,
            (decimal)progress,
            destinationReached: false);

        Assert.Equal(expectedFare, result.ActualFare);
        Assert.Equal(expectedFare, result.FinalFare);
    }

    [Fact]
    public void FinalizeFare_CustomerStopAtDestination_StillUsesAuthoritativePlannedProgress()
    {
        var result = CreateService().CalculateLockedFare(
            CreateSnapshottedTrip(),
            TripEndReason.CUSTOMER_REQUESTED_STOP,
            plannedRouteProgress: 0.4m,
            destinationReached: true);

        Assert.Equal(48_000m, result.ActualFare);
    }

    [Theory]
    [InlineData(0d, 30_000, 30_000, 0)]
    [InlineData(0.1d, 30_000, 28_000, 2_000)]
    [InlineData(0.49d, 49_000, 39_200, 9_800)]
    [InlineData(0.5d, 100_000, 80_000, 20_000)]
    [InlineData(0.75d, 100_000, 80_000, 20_000)]
    [InlineData(1d, 100_000, 80_000, 20_000)]
    public void CustomerRequestedStopComponentAllocation_UsesProgressBelowHalfAndFullComponentsAtOrAboveHalf(
        double progress,
        decimal expectedGrossFare,
        decimal expectedFareComponent,
        decimal expectedLongDistanceComponent)
    {
        var allocation =
            TripFareFinalizationService.CalculateCustomerRequestedStopComponentAllocation(
                CreateSnapshottedTrip(
                    estimatedFare: 100_000m,
                    longDistanceComponent: 20_000m).Booking,
                (decimal)progress);

        Assert.Equal(expectedGrossFare, allocation.GrossFare);
        Assert.Equal(expectedFareComponent, allocation.FareComponent);
        Assert.Equal(expectedLongDistanceComponent, allocation.LongDistanceComponent);
        Assert.Equal(
            allocation.GrossFare,
            allocation.FareComponent + allocation.LongDistanceComponent);
    }

    [Fact]
    public void CustomerRequestedStopComponentAllocation_BelowHalf_RoundsApprovedGrossBeforeAllocatingComponents()
    {
        var allocation =
            TripFareFinalizationService.CalculateCustomerRequestedStopComponentAllocation(
                CreateSnapshottedTrip(
                    estimatedFare: 30_002m,
                    longDistanceComponent: 20_001m,
                    acceptedMinimumServiceFare: 0m).Booking,
                plannedRouteProgress: 0.49m);

        Assert.Equal(14_701m, allocation.GrossFare);
        Assert.Equal(4_901m, allocation.FareComponent);
        Assert.Equal(9_800m, allocation.LongDistanceComponent);
        Assert.Equal(
            allocation.GrossFare,
            allocation.FareComponent + allocation.LongDistanceComponent);
        Assert.Equal(
            decimal.Round(30_002m * 0.49m, 0, MidpointRounding.AwayFromZero),
            allocation.GrossFare);
    }

    [Fact]
    public void FinalizeFare_CustomerRequestedStop_IgnoresMutableCurrentLongDistanceConfiguration()
    {
        var trip = CreateSnapshottedTrip(
            estimatedFare: 100_000m,
            longDistanceComponent: 20_000m);
        trip.Booking.PricingRule!.BaseFare = 1m;
        trip.Booking.PricingRule.PricePerKm = 1m;

        var result = CreateService(new DriverCompensationOptions
        {
            LongDistanceThresholdKm = 999,
            LongDistanceOptInThresholdKm = 999,
            LongDistanceRatePerKm = 1m,
            DestinationReachedThresholdMeters = 250
        }).CalculateLockedFare(
            trip,
            TripEndReason.CUSTOMER_REQUESTED_STOP,
            plannedRouteProgress: 0.5m,
            destinationReached: false);

        Assert.Equal(100_000m, result.ActualFare);
    }

    [Fact]
    public void FinalizeFare_CustomerRequestedStop_IgnoresActualDistance()
    {
        var shortPathTrip = CreateSnapshottedTrip(
            estimatedFare: 100_000m,
            longDistanceComponent: 20_000m);
        var longPathTrip = CreateSnapshottedTrip(
            estimatedFare: 100_000m,
            longDistanceComponent: 20_000m);
        shortPathTrip.ActualDistanceKm = 0.01m;
        longPathTrip.ActualDistanceKm = 999m;

        var service = CreateService();
        var shortPathResult = service.CalculateLockedFare(
            shortPathTrip,
            TripEndReason.CUSTOMER_REQUESTED_STOP,
            plannedRouteProgress: 0.5m,
            destinationReached: false);
        var longPathResult = service.CalculateLockedFare(
            longPathTrip,
            TripEndReason.CUSTOMER_REQUESTED_STOP,
            plannedRouteProgress: 0.5m,
            destinationReached: false);

        Assert.Equal(100_000m, shortPathResult.ActualFare);
        Assert.Equal(shortPathResult, longPathResult);
    }

    [Fact]
    public void FinalizeFare_CustomerRequestedStopAtHalfProgress_PreservesPromotionHandling()
    {
        var trip = CreateSnapshottedTrip();
        trip.Booking.BookingPromotions.Add(new BookingPromotion
        {
            DiscountAmount = 10_000m
        });

        var result = CreateService().CalculateLockedFare(
            trip,
            TripEndReason.CUSTOMER_REQUESTED_STOP,
            plannedRouteProgress: 0.5m,
            destinationReached: false);

        Assert.Equal(120_000m, result.ActualFare);
        Assert.Equal(110_000m, result.FinalFare);
    }

    [Theory]
    [InlineData(TripEndReason.DRIVER_UNABLE_TO_CONTINUE)]
    [InlineData(TripEndReason.STARTED_BY_MISTAKE)]
    public void FinalizeFare_NoCustomerChargeReasons_ReturnZero(
        TripEndReason reason)
    {
        var result = CreateService().CalculateLockedFare(
            CreateSnapshottedTrip(),
            reason,
            plannedRouteProgress: 0.8m,
            destinationReached: false);

        Assert.Equal(0m, result.ActualFare);
        Assert.Equal(0m, result.FinalFare);
    }

    [Fact]
    public void FinalizeFare_SystemError_RequiresAuthorizedReconciliation()
    {
        var exception = Assert.Throws<BookingException>(() =>
            CreateService().CalculateLockedFare(
                CreateSnapshottedTrip(),
                TripEndReason.SYSTEM_ERROR,
                plannedRouteProgress: 0.8m,
                destinationReached: false));

        Assert.Equal("trip.system_error_reconciliation_required", exception.Code);
    }

    [Theory]
    [InlineData(249, true)]
    [InlineData(250, true)]
    [InlineData(250.001, false)]
    public void DestinationReached_UsesConfiguredInclusiveThreshold(
        double distanceMeters,
        bool expected)
    {
        Assert.Equal(expected, CreateService().IsDestinationReached(distanceMeters));
    }

    [Theory]
    [InlineData(TripEndReason.VEHICLE_SAFETY_ISSUE)]
    [InlineData(TripEndReason.SAFETY_TERMINATION)]
    public void FinalizeFare_SafetyReason_RequiresRiskProtectionFlow(
        TripEndReason reason)
    {
        var exception = Assert.Throws<BookingException>(() =>
            CreateService().CalculateLockedFare(
                CreateSnapshottedTrip(),
                reason,
                plannedRouteProgress: 0.5m,
                destinationReached: false));

        Assert.Equal("trip.safety_termination_required", exception.Code);
    }

    [Fact]
    public void CalculateFare_HourlyRuleWithZeroUnitPrice_RejectsInvalidRule()
    {
        var service = new FareEstimationService();
        var rule = new PricingRule
        {
            BaseFare = 20_000m,
            MinFare = 30_000m,
            PricePerHour = 0m
        };

        var exception = Assert.Throws<BookingException>(
            () => service.CalculateFare(rule, distanceKm: 0, durationMinutes: 120));

        Assert.Equal("booking.invalid_pricing_rule", exception.Code);
    }

    [Fact]
    public void CalculateFare_ValidHourlyRule_ReturnsPositiveFare()
    {
        var service = new FareEstimationService();
        var rule = new PricingRule
        {
            BaseFare = 20_000m,
            MinFare = 30_000m,
            PricePerHour = 60_000m
        };

        var fare = service.CalculateFare(rule, distanceKm: 0, durationMinutes: 90);

        Assert.Equal(110_000m, fare);
    }

    [Fact]
    public void FinalizeFare_LegacyV0_RemainsOnIsolatedActualDistancePath()
    {
        var pricingRule = new PricingRule
        {
            BaseFare = 20_000m,
            MinFare = 30_000m,
            PricePerKm = 10_000m
        };
        var booking = new Booking
        {
            EstimatedFare = 72_000m,
            PricingRule = pricingRule
        };
        booking.BookingPromotions.Add(new BookingPromotion
        {
            DiscountAmount = 10_000m
        });
        var trip = new Trip { Booking = booking };
        var service = CreateService();

        var result = service.Calculate(
            trip,
            actualDistanceKm: 2m,
            actualDurationMinutes: 5);

        Assert.Equal(40_000m, result.ActualFare);
        Assert.Equal(30_000m, result.FinalFare);
        Assert.NotEqual(booking.EstimatedFare, result.FinalFare);
    }

    private static TripFareFinalizationService CreateService(
        DriverCompensationOptions? compensationOptions = null) => new(
        new FareEstimationService(),
        Options.Create(compensationOptions ?? new DriverCompensationOptions
        {
            DestinationReachedThresholdMeters = 250
        }));

    private static Trip CreateSnapshottedTrip(
        decimal estimatedFare = 120_000m,
        decimal longDistanceComponent = 0m,
        decimal acceptedMinimumServiceFare = 30_000m) => new()
    {
        Booking = new Booking
        {
            PricingSnapshotVersion = Booking.CurrentPricingSnapshotVersion,
            EstimatedFare = estimatedFare,
            AcceptedMinimumServiceFare = acceptedMinimumServiceFare,
            SurgedFare = estimatedFare - longDistanceComponent,
            LongDistanceComponent = longDistanceComponent,
            AcceptedLongDistanceThresholdKm = 15m,
            AcceptedLongDistanceRatePerKm = 3_000m,
            PricingRule = new PricingRule
            {
                BaseFare = 999_999m,
                MinFare = 888_888m,
                PricePerKm = 777_777m
            },
            SurgePricingRule = new SurgePricingRule
            {
                SurgeMultiplier = 9m
            }
        }
    };
}

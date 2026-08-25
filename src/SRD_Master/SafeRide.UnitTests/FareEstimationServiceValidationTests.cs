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
    [InlineData(0.5, 60_000)]
    public void FinalizeFare_CustomerRequestedStop_UsesProgressWithSnapshotMinimum(
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
    public void FinalizeFare_CustomerStopWithinDestinationThreshold_UsesFullLockedFare()
    {
        var result = CreateService().CalculateLockedFare(
            CreateSnapshottedTrip(),
            TripEndReason.CUSTOMER_REQUESTED_STOP,
            plannedRouteProgress: 0.4m,
            destinationReached: true);

        Assert.Equal(120_000m, result.ActualFare);
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
        var service = new TripFareFinalizationService(new FareEstimationService());

        var result = service.Calculate(
            trip,
            actualDistanceKm: 2m,
            actualDurationMinutes: 5);

        Assert.Equal(40_000m, result.ActualFare);
        Assert.Equal(30_000m, result.FinalFare);
        Assert.NotEqual(booking.EstimatedFare, result.FinalFare);
    }

    private static TripFareFinalizationService CreateService() => new(
        new FareEstimationService(),
        Options.Create(new DriverCompensationOptions
        {
            DestinationReachedThresholdMeters = 250
        }));

    private static Trip CreateSnapshottedTrip() => new()
    {
        Booking = new Booking
        {
            PricingSnapshotVersion = Booking.CurrentPricingSnapshotVersion,
            EstimatedFare = 120_000m,
            AcceptedMinimumServiceFare = 30_000m,
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

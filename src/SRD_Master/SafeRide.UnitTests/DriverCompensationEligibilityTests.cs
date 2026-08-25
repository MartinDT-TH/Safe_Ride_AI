using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Drivers.Services;
using SafeRide.Domain.Entities;

namespace SafeRide.UnitTests;

public sealed class DriverCompensationEligibilityTests
{
    private static readonly DriverCompensationOptions Options = new()
    {
        LongPickupThresholdKm = 5,
        LongPickupOptInThresholdKm = 8,
        LongDistanceThresholdKm = 15,
        LongDistanceOptInThresholdKm = 30,
        MaximumTripDistanceKm = 50,
        LongPickupRatePerKm = 3_000m,
        LongDistanceRatePerKm = 3_000m
    };

    [Theory]
    [InlineData(5, 0)]
    [InlineData(7.5, 7500)]
    [InlineData(8, 9000)]
    public void CalculateLongPickupCompensation_UsesThresholdAndWholeVnd(
        decimal pickupDistanceKm,
        decimal expectedCompensation)
    {
        var compensation = DriverCompensationEligibility.CalculateLongPickupCompensation(
            pickupDistanceKm,
            Options);

        Assert.Equal(expectedCompensation, compensation);
        Assert.Equal(
            pickupDistanceKm > 8m,
            DriverCompensationEligibility.RequiresLongPickupOptIn(
                pickupDistanceKm,
                Options));
    }

    [Fact]
    public void LongDistanceEligibility_UsesImmutableDistance_AndExemptsHourlyBookings()
    {
        var ordinary = new Booking { EstimatedDistanceKm = 30m };
        var optIn = new Booking { EstimatedDistanceKm = 30.001m };
        var maximum = new Booking { EstimatedDistanceKm = 50.001m };
        var hourly = new Booking
        {
            EstimatedDistanceKm = 80m,
            AcceptedPricePerHour = 100_000m
        };

        Assert.False(DriverCompensationEligibility.RequiresLongDistanceOptIn(ordinary, Options));
        Assert.True(DriverCompensationEligibility.RequiresLongDistanceOptIn(optIn, Options));
        Assert.True(DriverCompensationEligibility.ExceedsMaximumTripDistance(maximum, Options));
        Assert.False(DriverCompensationEligibility.RequiresLongDistanceOptIn(hourly, Options));
        Assert.False(DriverCompensationEligibility.ExceedsMaximumTripDistance(hourly, Options));
    }

    [Fact]
    public void LongDistanceEligibility_V1UsesAcceptedThresholds_NotCurrentOptions()
    {
        var acceptedOptions = new DriverCompensationOptions
        {
            LongDistanceOptInThresholdKm = 45,
            MaximumTripDistanceKm = 45
        };
        var changedOptions = new DriverCompensationOptions
        {
            LongDistanceOptInThresholdKm = 30,
            MaximumTripDistanceKm = 35
        };
        var booking = new Booking
        {
            PricingSnapshotVersion = Booking.CurrentPricingSnapshotVersion,
            EstimatedDistanceKm = Convert.ToDecimal(
                acceptedOptions.LongDistanceOptInThresholdKm),
            AcceptedLongDistanceOptInThresholdKm = Convert.ToDecimal(
                acceptedOptions.LongDistanceOptInThresholdKm),
            AcceptedMaximumTripDistanceKm = Convert.ToDecimal(
                acceptedOptions.MaximumTripDistanceKm)
        };

        Assert.False(DriverCompensationEligibility.RequiresLongDistanceOptIn(
            booking,
            changedOptions));
        Assert.False(DriverCompensationEligibility.ExceedsMaximumTripDistance(
            booking,
            changedOptions));

        booking.EstimatedDistanceKm += 0.001m;

        Assert.True(DriverCompensationEligibility.RequiresLongDistanceOptIn(
            booking,
            changedOptions));
        Assert.True(DriverCompensationEligibility.ExceedsMaximumTripDistance(
            booking,
            changedOptions));
    }
}

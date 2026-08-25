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
}

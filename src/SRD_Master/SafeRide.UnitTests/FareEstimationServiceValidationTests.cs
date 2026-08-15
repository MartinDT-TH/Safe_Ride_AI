using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Services;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Services;

namespace SafeRide.UnitTests;

public sealed class FareEstimationServiceValidationTests
{
    [Fact]
    public void FinalizeFare_WhenSixtyPercentOfRouteCompleted_ChargesSixtyPercent()
    {
        var trip = new SafeRide.Domain.Entities.Trip
        {
            Booking = new SafeRide.Domain.Entities.Booking
            {
                EstimatedDistanceKm = 10m,
                EstimatedFare = 100_000m
            }
        };
        var service = new TripFareFinalizationService(new FareEstimationService());

        var result = service.Calculate(trip, 6m, 10);

        Assert.Equal(60_000m, result.ActualFare);
        Assert.Equal(60_000m, result.FinalFare);
    }

    [Fact]
    public void FinalizeFare_WhenEarlyEndHasZeroDistance_UsesMinimumFare()
    {
        var trip = new Trip
        {
            Booking = new Booking
            {
                EstimatedDistanceKm = 10m,
                EstimatedFare = 100_000m
            }
        };
        var service = new TripFareFinalizationService(new FareEstimationService());

        var result = service.Calculate(
            trip,
            actualDistanceKm: 0m,
            actualDurationMinutes: 0,
            minimumFare: 2_000m);

        Assert.Equal(2_000m, result.ActualFare);
        Assert.Equal(2_000m, result.FinalFare);
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
    public void FinalizeFare_EarlyPerKilometerTrip_UsesActualDistance()
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
}

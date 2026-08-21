using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Services;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Services;

namespace SafeRide.UnitTests;

public sealed class FareEstimationServiceValidationTests
{
    [Fact]
    public void FinalizeFare_WhenPerKilometerTripHasNoMovement_ReturnsZero()
    {
        var pricingRule = new PricingRule
        {
            BaseFare = 20_000m,
            MinFare = 50_000m,
            PricePerKm = 5_000m
        };
        var trip = new Trip
        {
            Booking = new Booking
            {
                EstimatedDistanceKm = 10m,
                EstimatedFare = 100_000m,
                PricingRule = pricingRule
            }
        };
        trip.Booking.BookingPromotions.Add(new BookingPromotion
        {
            DiscountAmount = 10_000m
        });
        var service = new TripFareFinalizationService(new FareEstimationService());

        var result = service.Calculate(trip, 0m, 1);

        Assert.Equal(0m, result.ActualFare);
        Assert.Equal(0m, result.FinalFare);
    }

    [Fact]
    public void FinalizeFare_WhenTripEndsEarly_PreservesMinimumFare()
    {
        var pricingRule = new PricingRule
        {
            BaseFare = 20_000m,
            MinFare = 50_000m,
            PricePerKm = 5_000m
        };
        var trip = new Trip
        {
            Booking = new Booking
            {
                EstimatedDistanceKm = 10m,
                EstimatedFare = 100_000m,
                PricingRule = pricingRule
            }
        };
        var service = new TripFareFinalizationService(new FareEstimationService());

        var result = service.Calculate(
            trip,
            actualDistanceKm: 1m,
            actualDurationMinutes: 10);

        Assert.Equal(50_000m, result.ActualFare);
        Assert.Equal(50_000m, result.FinalFare);
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

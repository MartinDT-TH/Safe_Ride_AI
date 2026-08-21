using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Services;

public sealed class TripFareFinalizationService
{
    private readonly IFareEstimationService _fareEstimationService;

    public TripFareFinalizationService(IFareEstimationService fareEstimationService)
    {
        _fareEstimationService = fareEstimationService;
    }

    public TripFareFinalizationResult Calculate(
        Trip trip,
        decimal actualDistanceKm,
        int actualDurationMinutes,
        decimal minimumFare = 0m)
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

    private static decimal RoundVnd(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}

public sealed record TripFareFinalizationResult(
    decimal ActualFare,
    decimal FinalFare);

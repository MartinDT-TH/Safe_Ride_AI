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
        int actualDurationMinutes)
    {
        var actualFare = trip.Booking.PricingRule is null
            ? trip.Booking.EstimatedFare
            : _fareEstimationService.CalculateFare(
                trip.Booking.PricingRule,
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

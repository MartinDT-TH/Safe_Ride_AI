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
        var discountAmount = trip.Booking.BookingPromotions.Sum(x => x.DiscountAmount);
        var estimatedDistanceKm = trip.Booking.EstimatedDistanceKm;
        if (estimatedDistanceKm.HasValue
            && estimatedDistanceKm.Value > 0m
            && (actualDistanceKm > 0m || minimumFare > 0m)
            && actualDistanceKm < estimatedDistanceKm.Value)
        {
            var completedRatio = decimal.Clamp(
                actualDistanceKm / estimatedDistanceKm.Value,
                0m,
                1m);
            var proportionalActualFare = Math.Max(
                minimumFare,
                RoundVnd(trip.Booking.EstimatedFare * completedRatio));
            var originalFinalFare = Math.Max(
                0m,
                trip.Booking.EstimatedFare - discountAmount);
            var proportionalFinalFare = Math.Max(
                minimumFare,
                RoundVnd(originalFinalFare * completedRatio));

            return new TripFareFinalizationResult(
                proportionalActualFare,
                proportionalFinalFare);
        }

        var actualFare = trip.Booking.PricingRule is null
            ? trip.Booking.EstimatedFare
            : _fareEstimationService.CalculateFare(
                trip.Booking.PricingRule,
                actualDistanceKm,
                actualDurationMinutes,
                trip.Booking.SurgePricingRule);

        actualFare = RoundVnd(actualFare);
        var finalFare = RoundVnd(Math.Max(0m, actualFare - discountAmount));

        return new TripFareFinalizationResult(actualFare, finalFare);
    }

    private static decimal RoundVnd(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}

public sealed record TripFareFinalizationResult(
    decimal ActualFare,
    decimal FinalFare);

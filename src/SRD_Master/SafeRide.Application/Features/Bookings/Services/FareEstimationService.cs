using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.Bookings.Services;

public sealed class FareEstimationService : IFareEstimationService
{
    public BookingFareBreakdown CalculateBookingFare(
        PricingRule pricingRule,
        decimal distanceKm,
        int durationMinutes,
        decimal surgeMultiplier,
        DriverCompensationOptions compensationOptions)
    {
        if (distanceKm < 0m || durationMinutes < 0 || surgeMultiplier < 1m)
        {
            throw InvalidPricingRule();
        }

        var isDistanceBased = pricingRule.PricePerKm is > 0m
            && !pricingRule.PricePerHour.HasValue;
        var isHourly = pricingRule.PricePerHour is > 0m
            && !pricingRule.PricePerKm.HasValue;

        if (!isDistanceBased && !isHourly)
        {
            throw InvalidPricingRule();
        }

        if (isDistanceBased
            && distanceKm > Convert.ToDecimal(compensationOptions.MaximumTripDistanceKm))
        {
            throw new BookingException(
                "booking.maximum_trip_distance_exceeded",
                "Chuyến đi vượt quá khoảng cách tối đa được hỗ trợ.",
                400);
        }

        var variableFare = isDistanceBased
            ? distanceKm * pricingRule.PricePerKm!.Value
            : ((decimal)durationMinutes / 60m) * pricingRule.PricePerHour!.Value;
        var rawFare = pricingRule.BaseFare + variableFare;

        var normalFare = RoundMoney(Math.Max(pricingRule.MinFare, rawFare));
        var surgedFare = RoundMoney(Math.Max(
            pricingRule.MinFare,
            rawFare * surgeMultiplier));
        var surgeAmount = surgedFare - normalFare;

        var longDistanceComponent = isDistanceBased
            ? RoundMoney(
                Math.Max(
                    0m,
                    distanceKm - Convert.ToDecimal(
                        compensationOptions.LongDistanceThresholdKm))
                * compensationOptions.LongDistanceRatePerKm)
            : 0m;
        var estimatedFare = surgedFare + longDistanceComponent;

        if (estimatedFare <= 0m)
        {
            throw new BookingException(
                "booking.invalid_pricing_rule",
                "Cấu hình giá của dịch vụ không thể tạo ra giá chuyến hợp lệ.",
                500);
        }

        return new BookingFareBreakdown(
            normalFare,
            surgedFare,
            surgeAmount,
            longDistanceComponent,
            estimatedFare);
    }

    public decimal CalculateFare(
        PricingRule pricingRule,
        decimal distanceKm,
        int durationMinutes,
        SurgePricingRule? surgeRule = null)
    {
        decimal rawFare;

        if (pricingRule.PricePerKm is > 0m && !pricingRule.PricePerHour.HasValue)
        {
            rawFare = pricingRule.BaseFare
                + distanceKm * pricingRule.PricePerKm.Value;
        }
        else if (pricingRule.PricePerHour is > 0m && !pricingRule.PricePerKm.HasValue)
        {
            var estimatedHours = (decimal)durationMinutes / 60m;
            rawFare = pricingRule.BaseFare
                + estimatedHours * pricingRule.PricePerHour.Value;
        }
        else
        {
            throw new BookingException(
                "booking.invalid_pricing_rule",
                "Cấu hình giá của dịch vụ không hợp lệ.",
                500);
        }

        var multiplier = surgeRule?.SurgeMultiplier ?? 1.00m;
        var finalFare = rawFare * multiplier;
        var minFareWithSurge = pricingRule.MinFare * multiplier;

        var roundedFare = decimal.Round(
            Math.Max(minFareWithSurge, finalFare),
            2,
            MidpointRounding.AwayFromZero);

        if (roundedFare <= 0)
        {
            throw new BookingException(
                "booking.invalid_pricing_rule",
                "Cấu hình giá của dịch vụ không thể tạo ra giá chuyến hợp lệ.",
                500);
        }

        return roundedFare;
    }

    private static decimal RoundMoney(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    private static BookingException InvalidPricingRule() =>
        new(
            "booking.invalid_pricing_rule",
            "Cấu hình giá của dịch vụ không hợp lệ.",
            500);
}

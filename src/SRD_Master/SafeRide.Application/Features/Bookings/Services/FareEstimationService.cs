using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.Bookings.Services;

public sealed class FareEstimationService : IFareEstimationService
{
    public decimal CalculateFare(
        PricingRule pricingRule,
        decimal distanceKm,
        int durationMinutes)
    {
        decimal rawFare;

        if (pricingRule.PricePerKm.HasValue && !pricingRule.PricePerHour.HasValue)
        {
            rawFare = pricingRule.BaseFare
                + distanceKm * pricingRule.PricePerKm.Value;
        }
        else if (pricingRule.PricePerHour.HasValue && !pricingRule.PricePerKm.HasValue)
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

        return decimal.Round(
            Math.Max(pricingRule.MinFare, rawFare),
            2,
            MidpointRounding.AwayFromZero);
    }
}

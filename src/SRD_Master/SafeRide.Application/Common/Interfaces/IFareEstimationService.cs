using SafeRide.Domain.Entities;

namespace SafeRide.Application.Common.Interfaces;

public interface IFareEstimationService
{
    decimal CalculateFare(
        PricingRule pricingRule,
        decimal distanceKm,
        int durationMinutes,
        SurgePricingRule? surgeRule = null);
}

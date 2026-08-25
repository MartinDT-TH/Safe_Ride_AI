using SafeRide.Domain.Entities;
using SafeRide.Application.Common.Models;

namespace SafeRide.Application.Common.Interfaces;

public interface IFareEstimationService
{
    BookingFareBreakdown CalculateBookingFare(
        PricingRule pricingRule,
        decimal distanceKm,
        int durationMinutes,
        decimal surgeMultiplier,
        DriverCompensationOptions compensationOptions);

    // Legacy V0 compatibility path. Phase 2 will route V1 trip finalization
    // through the immutable booking snapshot instead of this mutable rule.
    decimal CalculateFare(
        PricingRule pricingRule,
        decimal distanceKm,
        int durationMinutes,
        SurgePricingRule? surgeRule = null);
}

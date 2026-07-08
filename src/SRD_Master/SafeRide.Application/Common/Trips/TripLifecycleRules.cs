using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Trips;

public static class TripLifecycleRules
{
    public static bool IsActive(TripStatus status)
    {
        return status is not TripStatus.COMPLETED and not TripStatus.CANCELLED;
    }
}

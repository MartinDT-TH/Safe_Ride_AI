using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.Services;

internal sealed class NoOpTripShareExpiryScheduler : ITripShareExpiryScheduler
{
    public void ScheduleExpiration(long tripShareId, DateTime expiresAt)
    {
    }
}

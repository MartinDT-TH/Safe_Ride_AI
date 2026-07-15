namespace SafeRide.Application.Common.Interfaces;

public interface ITripShareExpiryScheduler
{
    void ScheduleExpiration(long tripShareId, DateTime expiresAt);
}

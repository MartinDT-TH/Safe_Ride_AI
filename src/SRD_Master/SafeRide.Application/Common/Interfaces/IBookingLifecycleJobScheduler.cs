namespace SafeRide.Application.Common.Interfaces;

/// <summary>
/// Schedules and cancels Hangfire delayed jobs for the booking lifecycle
/// (radius expansion, booking expiry). Decouples Application layer from Hangfire.
/// </summary>
public interface IBookingLifecycleJobScheduler
{
    /// <summary>
    /// Schedules the expand-radius job to run after <paramref name="delay"/>.
    /// Stores the resulting Hangfire job ID in Redis so it can be cancelled later.
    /// </summary>
    void ScheduleExpandRadius(long bookingId, TimeSpan delay);

    /// <summary>
    /// Schedules the expire-booking job to run after <paramref name="delay"/>.
    /// Stores the resulting Hangfire job ID in Redis so it can be cancelled later.
    /// </summary>
    void ScheduleExpireBooking(long bookingId, TimeSpan delay);

    /// <summary>
    /// Schedules the driver-offer expiry job to run after <paramref name="delay"/>.
    /// Stores the resulting Hangfire job ID in Redis so it can be cancelled later.
    /// </summary>
    void ScheduleExpireDriverOffer(long offerId, TimeSpan delay);

    /// <summary>
    /// Deletes the scheduled driver-offer expiry job if it still exists.
    /// </summary>
    Task CancelExpireDriverOfferAsync(long offerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the scheduled expand-radius, expire-booking, and active-offer jobs for the given
    /// booking (if they still exist in Hangfire). Should be called when the booking
    /// leaves the Searching state before the timers fire.
    /// </summary>
    Task CancelJobsForBookingAsync(long bookingId, CancellationToken cancellationToken = default);
}

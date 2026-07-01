using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.Services;

internal sealed class NoOpBookingLifecycleJobScheduler : IBookingLifecycleJobScheduler
{
    private readonly ILogger<NoOpBookingLifecycleJobScheduler> _logger;

    public NoOpBookingLifecycleJobScheduler(ILogger<NoOpBookingLifecycleJobScheduler> logger)
    {
        _logger = logger;
    }

    public void ScheduleExpandRadius(long bookingId, TimeSpan delay)
    {
        _logger.LogDebug(
            "Background jobs disabled. Skipped scheduling expand-radius job for BookingId={BookingId}.",
            bookingId);
    }

    public void ScheduleExpireBooking(long bookingId, TimeSpan delay)
    {
        _logger.LogDebug(
            "Background jobs disabled. Skipped scheduling expire-booking job for BookingId={BookingId}.",
            bookingId);
    }

    public void ScheduleExpireDriverOffer(long offerId, TimeSpan delay)
    {
        _logger.LogDebug(
            "Background jobs disabled. Skipped scheduling expire-driver-offer job for OfferId={OfferId}.",
            offerId);
    }

    public Task CancelExpireDriverOfferAsync(
        long offerId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task CancelJobsForBookingAsync(
        long bookingId,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class ScheduledBookingMatchingJob : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledBookingMatchingJob> _logger;

    public ScheduledBookingMatchingJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduledBookingMatchingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);

        await ProcessScheduledBookingsAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessScheduledBookingsAsync(stoppingToken);
        }
    }

    private async Task ProcessScheduledBookingsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            var matchingService =
                scope.ServiceProvider.GetRequiredService<IBookingMatchingService>();
            var realtimeNotificationService =
                scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();

            var bookings = await repository.GetScheduledBookingsReadyForMatchingAsync(
                clock.UtcNow.AddMinutes(15),
                cancellationToken);
            if (bookings.Count == 0)
            {
                return;
            }

            foreach (var booking in bookings)
            {
                try
                {
                    booking.BookingStatus = BookingStatus.Searching;
                    booking.UpdatedAt = clock.UtcNow;

                    await matchingService.StartMatchingAsync(
                        booking.BookingId,
                        cancellationToken);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await realtimeNotificationService.PublishBookingStatusChangedAsync(
                        new BookingStatusChangedEvent(
                            booking.BookingId,
                            booking.CustomerId,
                            booking.BookingStatus,
                            booking.UpdatedAt),
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    // Keep the booking eligible for the next polling cycle.
                    booking.BookingStatus = BookingStatus.PendingSchedule;
                    _logger.LogError(
                        exception,
                        "Could not start matching for scheduled booking {BookingId}.",
                        booking.BookingId);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Scheduled booking matching cycle failed.");
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class ScheduledBookingMatchingJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ScheduledBookingMatchingOptions> _options;
    private readonly ILogger<ScheduledBookingMatchingJob> _logger;

    public ScheduledBookingMatchingJob(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ScheduledBookingMatchingOptions> options,
        ILogger<ScheduledBookingMatchingJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ProcessScheduledBookingsAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var pollingInterval = TimeSpan.FromSeconds(
                Math.Max(1, _options.CurrentValue.PollingIntervalSeconds));
            await Task.Delay(pollingInterval, stoppingToken);
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

            var scheduledOptions = _options.CurrentValue;
            var bookings = await repository.GetScheduledBookingsReadyForMatchingAsync(
                clock.UtcNow.AddMinutes(scheduledOptions.StartMatchingBeforeMinutes),
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

                    // Schedule Hangfire delayed jobs for lifecycle management.
                    var matchingOptions = scope.ServiceProvider
                        .GetRequiredService<IMatchingPolicyProvider>().Current;
                    var jobScheduler = scope.ServiceProvider
                        .GetRequiredService<IBookingLifecycleJobScheduler>();
                    jobScheduler.ScheduleExpandRadius(
                        booking.BookingId,
                        TimeSpan.FromMinutes(matchingOptions.ExpandAfterMinutes));
                    jobScheduler.ScheduleExpireBooking(
                        booking.BookingId,
                        TimeSpan.FromMinutes(matchingOptions.BookingExpireAfterMinutes));

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

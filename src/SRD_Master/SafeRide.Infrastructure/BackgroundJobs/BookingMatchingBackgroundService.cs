using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class BookingMatchingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingMatchingBackgroundService> _logger;

    public BookingMatchingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingMatchingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessMatchingAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing booking matching logic.");
                }

                // Wait for next cycle based on config
                var delay = await GetDelayAsync(stoppingToken);
                await Task.Delay(delay, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        _logger.LogInformation("BookingMatchingBackgroundService stopped");
    }

    private async Task<TimeSpan> GetDelayAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var policyProvider = scope.ServiceProvider.GetRequiredService<IMatchingPolicyProvider>();
        return TimeSpan.FromSeconds(Math.Max(1, policyProvider.Current.MatchingTickSeconds));
    }

    private async Task ProcessMatchingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var matchingService = scope.ServiceProvider.GetRequiredService<IBookingMatchingService>();

        var bookingIdsToRetry = await GetSearchingBookingIdsAsync(
            dbContext,
            cancellationToken);

        // Flow: retry matching only for bookings still in Searching; expiry/expansion run in Hangfire jobs.
        foreach (var bookingId in bookingIdsToRetry)
        {
            await matchingService.StartMatchingAsync(bookingId, cancellationToken);
        }
    }

    private static Task<List<long>> GetSearchingBookingIdsAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Return just the IDs of bookings still actively searching so the
        // matching loop can keep firing offers. Expire / expand logic has been
        // moved to dedicated Hangfire delayed jobs.
        return dbContext.Bookings
            .Where(x => x.BookingStatus == BookingStatus.Searching)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.BookingId)
            .ToListAsync(cancellationToken);
    }
}

using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.Services;

public sealed class BookingMatchingService : IBookingMatchingService
{
    private readonly ILogger<BookingMatchingService> _logger;

    public BookingMatchingService(ILogger<BookingMatchingService> logger)
    {
        _logger = logger;
    }

    public Task StartMatchingAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Matching requested for booking {BookingId}.",
            bookingId);

        return Task.CompletedTask;
    }
}

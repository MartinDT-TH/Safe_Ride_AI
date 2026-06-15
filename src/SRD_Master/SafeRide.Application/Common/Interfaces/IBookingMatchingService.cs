namespace SafeRide.Application.Common.Interfaces;

public interface IBookingMatchingService
{
    Task StartMatchingAsync(long bookingId, CancellationToken cancellationToken);
}

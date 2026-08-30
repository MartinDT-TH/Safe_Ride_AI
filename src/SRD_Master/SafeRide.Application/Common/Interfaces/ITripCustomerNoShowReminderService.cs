namespace SafeRide.Application.Common.Interfaces;

public interface ITripCustomerNoShowReminderService
{
    Task<bool> RecordIfNeededAsync(long tripId, CancellationToken cancellationToken);
}

namespace SafeRide.Application.Common.Interfaces;

public interface ITripCustomerReadinessService
{
    Task ReportAsync(Guid customerId, long tripId, string message, CancellationToken cancellationToken);
}

using SafeRide.Application.Common.Models;

namespace SafeRide.Application.Common.Interfaces;

public interface ICustomerNoShowReportingService
{
    Task<CustomerNoShowReportResponse> ReportAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken);
}

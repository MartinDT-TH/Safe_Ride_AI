using SafeRide.Application.Common.Models;

namespace SafeRide.Application.Common.Interfaces;

public interface ITripArrivalVerificationService
{
    Task<TripArrivalVerificationResult> VerifyAndRecordAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken);
}

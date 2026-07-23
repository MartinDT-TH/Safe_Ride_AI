using SafeRide.Contracts.Responses.Feedbacks;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Common.Interfaces;

public interface IRatingRepository
{
    Task<Trip?> GetTripForRatingAsync(
        long tripId,
        CancellationToken cancellationToken = default);

    Task AddRatingAsync(
        Rating rating,
        CancellationToken cancellationToken = default);

    Task<bool> DriverExistsAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DriverRatingItemResponse>> GetDriverRatingItemsAsync(
        Guid driverId,
        CancellationToken cancellationToken = default);
}

using SafeRide.Application.Features.TripSharing;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface ITripSharingService
{
    Task<CreateTripShareResult> CreateAsync(
        long tripId,
        Guid sharedByUserId,
        string recipientPhoneNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripShareListItemDto>> ListAsync(
        long tripId,
        Guid sharedByUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReceivedTripShareListItemDto>> ListReceivedAsync(
        Guid recipientUserId,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<ResolveTripShareResult> ResolveAsync(
        string rawToken,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task<SharedTripTrackingDto> GetTrackingAsync(
        long tripShareId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        long tripId,
        long tripShareId,
        Guid sharedByUserId,
        CancellationToken cancellationToken = default);

    Task<bool> CanSubscribeAsync(
        long tripShareId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task PublishLocationAsync(
        long tripId,
        double latitude,
        double longitude,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);

    Task HandleTripLifecycleAsync(
        long tripId,
        TripStatus tripStatus,
        DateTime occurredAt,
        CancellationToken cancellationToken = default);
}

using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.TripSharing;
using SafeRide.Domain.Enums;

namespace SafeRide.IntegrationTests;

internal sealed class TripSharingServiceFake : ITripSharingService
{
    public List<(long TripId, TripStatus Status, DateTime OccurredAt)> LifecycleEvents { get; } = [];
    public List<(long TripId, double Latitude, double Longitude)> LocationEvents { get; } = [];

    public Task PublishLocationAsync(long tripId, double latitude, double longitude, DateTime updatedAt, CancellationToken cancellationToken = default)
    {
        LocationEvents.Add((tripId, latitude, longitude));
        return Task.CompletedTask;
    }

    public Task HandleTripLifecycleAsync(long tripId, TripStatus tripStatus, DateTime occurredAt, CancellationToken cancellationToken = default)
    {
        LifecycleEvents.Add((tripId, tripStatus, occurredAt));
        return Task.CompletedTask;
    }

    public Task<bool> CanSubscribeAsync(long tripShareId, Guid recipientUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<CreateTripShareResult> CreateAsync(long tripId, Guid sharedByUserId, string recipientPhoneNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TripShareListItemDto>> ListAsync(long tripId, Guid sharedByUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<ReceivedTripShareListItemDto>> ListReceivedAsync(Guid recipientUserId, bool activeOnly, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ResolveTripShareResult> ResolveAsync(string rawToken, Guid recipientUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<SharedTripTrackingDto> GetTrackingAsync(long tripShareId, Guid recipientUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task RevokeAsync(long tripId, long tripShareId, Guid sharedByUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

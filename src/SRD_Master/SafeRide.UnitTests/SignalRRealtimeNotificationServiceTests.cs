using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Realtime;
using SafeRide.Realtime;

namespace SafeRide.UnitTests;

public sealed class SignalRRealtimeNotificationServiceTests
{
    [Fact]
    public async Task PublishDriverLocationUpdatedAsync_WithActiveTrip_SendsOnlyToTripGroup()
    {
        var clients = new RecordingHubClients();
        var service = new SignalRRealtimeNotificationService(
            new HubContextFake(clients));
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        await service.PublishDriverLocationUpdatedAsync(
            new DriverLocationUpdatedEvent(
                driverId,
                customerId,
                42,
                10.762622,
                106.660172,
                DateTime.UtcNow));

        var send = Assert.Single(clients.Sends);
        Assert.Equal(RealtimeGroups.Trip(42), send.GroupName);
        Assert.Equal("DriverLocationUpdated", send.Method);
    }

    [Fact]
    public async Task PublishDriverLocationUpdatedAsync_WithoutActiveTrip_SendsToDriverGroup()
    {
        var clients = new RecordingHubClients();
        var service = new SignalRRealtimeNotificationService(
            new HubContextFake(clients));
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        await service.PublishDriverLocationUpdatedAsync(
            new DriverLocationUpdatedEvent(
                driverId,
                CustomerId: null,
                TripId: null,
                10.762622,
                106.660172,
                DateTime.UtcNow));

        var send = Assert.Single(clients.Sends);
        Assert.Equal(RealtimeGroups.Driver(driverId), send.GroupName);
        Assert.Equal("DriverLocationUpdated", send.Method);
    }

    private sealed record RecordedSend(string GroupName, string Method);

    private sealed class HubContextFake(RecordingHubClients clients)
        : IHubContext<SafeRideHub>
    {
        public IHubClients Clients { get; } = clients;

        public IGroupManager Groups { get; } = new GroupManagerFake();
    }

    private sealed class RecordingHubClients : IHubClients
    {
        public List<RecordedSend> Sends { get; } = [];

        public IClientProxy All => new ClientProxyFake("*", Sends);

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) =>
            new ClientProxyFake("*", Sends);

        public IClientProxy Client(string connectionId) =>
            new ClientProxyFake(connectionId, Sends);

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) =>
            new ClientProxyFake(string.Join(",", connectionIds), Sends);

        public IClientProxy Group(string groupName) =>
            new ClientProxyFake(groupName, Sends);

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) =>
            new ClientProxyFake(groupName, Sends);

        public IClientProxy Groups(IReadOnlyList<string> groupNames) =>
            new ClientProxyFake(string.Join(",", groupNames), Sends);

        public IClientProxy User(string userId) =>
            new ClientProxyFake(userId, Sends);

        public IClientProxy Users(IReadOnlyList<string> userIds) =>
            new ClientProxyFake(string.Join(",", userIds), Sends);
    }

    private sealed class ClientProxyFake(
        string groupName,
        List<RecordedSend> sends)
        : IClientProxy
    {
        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            sends.Add(new RecordedSend(groupName, method));
            return Task.CompletedTask;
        }
    }

    private sealed class GroupManagerFake : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

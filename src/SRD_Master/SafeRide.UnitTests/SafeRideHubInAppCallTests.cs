using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Realtime;

namespace SafeRide.UnitTests;

public sealed class SafeRideHubInAppCallTests
{
    [Fact]
    public async Task SendInAppCallOffer_RelaysOfferToOtherClientsInTripGroup()
    {
        var clients = new RecordingHubCallerClients();
        var hub = CreateHub(clients);
        var signal = new InAppCallSignal(
            TripId: 42,
            BookingId: 24,
            CallId: "call-1",
            Sdp: "offer-sdp",
            SdpType: "offer",
            Candidate: null,
            SdpMid: null,
            SdpMLineIndex: null);

        await hub.SendInAppCallOffer(signal);

        var send = Assert.Single(clients.Sends);
        Assert.Equal(RealtimeGroups.Trip(42), send.Target);
        Assert.Equal("InAppCallOffer", send.Method);
        var payload = Assert.IsType<InAppCallSignal>(Assert.Single(send.Args));
        Assert.Equal(signal, payload);
    }

    [Fact]
    public async Task SendInAppCallAnswer_RelaysAnswerToOtherClientsInTripGroup()
    {
        var clients = new RecordingHubCallerClients();
        var hub = CreateHub(clients);

        await hub.SendInAppCallAnswer(
            new InAppCallSignal(
                TripId: 42,
                BookingId: 24,
                CallId: "call-1",
                Sdp: "answer-sdp",
                SdpType: "answer",
                Candidate: null,
                SdpMid: null,
                SdpMLineIndex: null));

        var send = Assert.Single(clients.Sends);
        Assert.Equal(RealtimeGroups.Trip(42), send.Target);
        Assert.Equal("InAppCallAnswer", send.Method);
    }

    [Fact]
    public async Task SendInAppCallIceCandidate_RelaysCandidateToOtherClientsInTripGroup()
    {
        var clients = new RecordingHubCallerClients();
        var hub = CreateHub(clients);

        await hub.SendInAppCallIceCandidate(
            new InAppCallSignal(
                TripId: 42,
                BookingId: 24,
                CallId: "call-1",
                Sdp: null,
                SdpType: null,
                Candidate: "candidate:1",
                SdpMid: "0",
                SdpMLineIndex: 0));

        var send = Assert.Single(clients.Sends);
        Assert.Equal(RealtimeGroups.Trip(42), send.Target);
        Assert.Equal("InAppCallIceCandidate", send.Method);
    }

    [Theory]
    [InlineData(0, "call-1")]
    [InlineData(42, "")]
    [InlineData(42, "   ")]
    public async Task InAppCallSignalMethods_RejectInvalidSignal(
        long tripId,
        string callId)
    {
        var hub = CreateHub(new RecordingHubCallerClients());
        var signal = new InAppCallSignal(
            tripId,
            BookingId: 24,
            callId,
            Sdp: null,
            SdpType: null,
            Candidate: null,
            SdpMid: null,
            SdpMLineIndex: null);

        await Assert.ThrowsAsync<HubException>(
            () => hub.SendInAppCallOffer(signal));
    }

    private static SafeRideHub CreateHub(RecordingHubCallerClients clients)
    {
        return new SafeRideHub(
            new DriverRealtimeServiceFake(),
            new TripContinuationAccessServiceFake())
        {
            Clients = clients,
            Context = new HubCallerContextFake()
        };
    }

    private sealed record RecordedSend(
        string Target,
        string Method,
        object?[] Args);

    private sealed class RecordingHubCallerClients : IHubCallerClients
    {
        public List<RecordedSend> Sends { get; } = [];

        public IClientProxy All => new ClientProxyFake("all", Sends);

        public IClientProxy Caller => new ClientProxyFake("caller", Sends);

        public IClientProxy Others => new ClientProxyFake("others", Sends);

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) =>
            new ClientProxyFake("allExcept", Sends);

        public IClientProxy Client(string connectionId) =>
            new ClientProxyFake($"client:{connectionId}", Sends);

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) =>
            new ClientProxyFake($"clients:{string.Join(",", connectionIds)}", Sends);

        public IClientProxy Group(string groupName) =>
            new ClientProxyFake(groupName, Sends);

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) =>
            new ClientProxyFake(groupName, Sends);

        public IClientProxy Groups(IReadOnlyList<string> groupNames) =>
            new ClientProxyFake(string.Join(",", groupNames), Sends);

        public IClientProxy OthersInGroup(string groupName) =>
            new ClientProxyFake(groupName, Sends);

        public IClientProxy OthersInGroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) =>
            new ClientProxyFake(groupName, Sends);

        public IClientProxy User(string userId) =>
            new ClientProxyFake($"user:{userId}", Sends);

        public IClientProxy Users(IReadOnlyList<string> userIds) =>
            new ClientProxyFake($"users:{string.Join(",", userIds)}", Sends);
    }

    private sealed class ClientProxyFake(
        string target,
        List<RecordedSend> sends)
        : IClientProxy
    {
        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            sends.Add(new RecordedSend(target, method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class HubCallerContextFake : HubCallerContext
    {
        public override string ConnectionId { get; } = "connection-1";

        public override string? UserIdentifier { get; } = "user-1";

        public override ClaimsPrincipal? User { get; } =
            new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")]));

        public override IDictionary<object, object?> Items { get; } =
            new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } =
            new FeatureCollection();

        public override CancellationToken ConnectionAborted { get; } =
            CancellationToken.None;

        public override void Abort()
        {
        }
    }

    private sealed class DriverRealtimeServiceFake : IDriverRealtimeService
    {
        public Task UpdateDriverLocationAsync(
            Guid driverId,
            DriverLocationUpdateInput location,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateDriverLocationAsync(
            Guid driverId,
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetDriverOnlineAsync(
            Guid driverId,
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetDriverOfflineAsync(
            Guid driverId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveDriverFromOnlineGeoAsync(
            Guid driverId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TripContinuationAccessServiceFake
        : ITripContinuationAccessService
    {
        public Task<bool> IsAllowedAsync(
            ClaimsPrincipal user,
            SafeRide.Application.Features.Auth.TripContinuationOperation operation,
            long? tripId = null,
            long? bookingId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}

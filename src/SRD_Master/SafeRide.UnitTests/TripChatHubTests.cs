using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Realtime;

namespace SafeRide.UnitTests;

public sealed class TripChatHubTests
{
    [Fact]
    public async Task SendTripMessage_CallsServiceWithAuthenticatedUserAndBroadcastsPayload()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tripId = 4L;
        var payload = new TripChatMessageDto(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            tripId,
            userId,
            "Khách hàng",
            "Text",
            "Anh tới chưa?",
            null,
            new DateTime(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc));
        var service = new TripChatServiceFake(payload);
        var clients = new RecordingHubCallerClients();
        var hub = new TripChatHub(service)
        {
            Clients = clients,
            Context = new HubCallerContextFake(userId),
            Groups = new GroupManagerFake()
        };

        await hub.SendTripMessage(tripId, "  Anh tới chưa?  ");

        Assert.Equal(userId, service.LastSenderUserId);
        Assert.Equal(tripId, service.LastTripId);
        Assert.Equal("  Anh tới chưa?  ", service.LastMessage);
        var send = Assert.Single(clients.Sends);
        Assert.Equal(RealtimeGroups.TripChat(tripId), send.Target);
        Assert.Equal("TripMessageReceived", send.Method);
        Assert.Equal(payload, Assert.Single(send.Args));
    }

    private sealed class TripChatServiceFake(TripChatMessageDto payload)
        : ITripChatService
    {
        public Guid LastSenderUserId { get; private set; }

        public long LastTripId { get; private set; }

        public string? LastMessage { get; private set; }

        public Task EnsureCanAccessTripChatAsync(
            Guid userId,
            long tripId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<TripChatMessageDto> SendMessageAsync(
            Guid senderUserId,
            long tripId,
            string message,
            CancellationToken cancellationToken = default)
        {
            LastSenderUserId = senderUserId;
            LastTripId = tripId;
            LastMessage = message;
            return Task.FromResult(payload);
        }

        public Task<TripChatMessageDto> SendImageMessageAsync(
            Guid senderUserId,
            long tripId,
            Stream image,
            string contentType,
            long fileSizeBytes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(payload);

        public Task<IReadOnlyList<TripChatMessageDto>> GetMessagesAsync(
            Guid userId,
            long tripId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TripChatMessageDto>>([]);

        public Task ShortenMessageTtlAsync(
            long tripId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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

    private sealed class HubCallerContextFake(Guid userId) : HubCallerContext
    {
        public override string ConnectionId { get; } = "connection-1";

        public override string? UserIdentifier { get; } = userId.ToString();

        public override ClaimsPrincipal? User { get; } =
            new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())]));

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
}

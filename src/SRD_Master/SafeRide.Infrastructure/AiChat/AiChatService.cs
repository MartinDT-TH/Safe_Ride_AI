using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.AiChat;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.AiChat;

public sealed class AiChatService : IAiChatService
{
    private readonly AiChatOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IRedisService _redis;
    private readonly IMapGeocodingService _geocoding;
    private readonly IMongoCollection<AiConversationDocument> _conversations;
    private readonly IMongoCollection<AiMessageDocument> _messages;

    public AiChatService(
        IOptions<AiChatOptions> options,
        HttpClient httpClient,
        IRedisService redis,
        IMapGeocodingService geocoding)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _redis = redis;
        _geocoding = geocoding;

        var client = new MongoClient(
            string.IsNullOrWhiteSpace(_options.MongoConnectionString)
                ? "mongodb://localhost:27017"
                : _options.MongoConnectionString);
        var database = client.GetDatabase(_options.MongoDatabase);
        _conversations = database.GetCollection<AiConversationDocument>("conversations");
        _messages = database.GetCollection<AiMessageDocument>("messages");
    }

    public async Task<AiChatReplyDto> SendAsync(
        Guid userId,
        SendAiChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var content = request.Message?.Trim() ?? "";
        if (content.Length is 0 || content.Length > _options.MaxMessageLength)
            throw new ArgumentException($"Tin nhắn phải có từ 1 đến {_options.MaxMessageLength} ký tự.");

        var now = DateTime.UtcNow;
        var expiresAt = now.AddDays(_options.RetentionDays);
        var conversation = await ResolveConversationAsync(
            userId, request.ConversationId, content, now, expiresAt, cancellationToken);

        var userMessage = NewMessage(conversation.Id, userId, "user", content, now, expiresAt);
        await _messages.InsertOneAsync(userMessage, cancellationToken: cancellationToken);
        await CacheMessageAsync(userId, conversation.Id, "user", content, cancellationToken);

        var context = await GetContextAsync(userId, conversation.Id, cancellationToken);
        var generated = await GenerateAsync(
            context, IsValidCurrentLocation(request.CurrentLocation), cancellationToken);
        var draftResolution = await ResolveBookingDraftAsync(
            generated, request.CurrentLocation, cancellationToken);
        var draft = draftResolution.Draft;
        if (draft is not null && draftResolution.UsedCurrentLocation)
        {
            generated = generated with
            {
                Reply = $"Mình đã dùng vị trí hiện tại làm điểm đón và ghi nhận điểm đến "
                    + $"“{generated.DestinationAddress}”. Bạn hãy kiểm tra lộ trình rồi tiếp tục đặt chuyến nhé."
            };
        }
        if (draft is null &&
            !string.IsNullOrWhiteSpace(generated.DestinationAddress) &&
            (draftResolution.UsedCurrentLocation ||
             !string.IsNullOrWhiteSpace(generated.PickupAddress)))
        {
            generated = generated with
            {
                Reply = BuildGeocodingFailureReply(generated, draftResolution)
            };
        }

        var assistantMessage = NewMessage(
            conversation.Id, userId, "assistant", generated.Reply, DateTime.UtcNow, expiresAt, draft);
        await _messages.InsertOneAsync(assistantMessage, cancellationToken: cancellationToken);
        await CacheMessageAsync(userId, conversation.Id, "assistant", generated.Reply, cancellationToken);

        await _conversations.UpdateOneAsync(
            item => item.Id == conversation.Id && item.UserId == userId,
            Builders<AiConversationDocument>.Update
                .Set(item => item.UpdatedAt, DateTime.UtcNow)
                .Set(item => item.ExpiresAt, expiresAt),
            cancellationToken: cancellationToken);

        return new AiChatReplyDto(
            conversation.Id.ToString(),
            Map(userMessage),
            Map(assistantMessage),
            draft);
    }

    public async Task<IReadOnlyList<AiConversationDto>> GetConversationsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return await _conversations.Find(item => item.UserId == userId)
            .SortByDescending(item => item.UpdatedAt)
            .Limit(30)
            .Project(item => new AiConversationDto(item.Id.ToString(), item.Title, item.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiChatMessageDto>> GetMessagesAsync(
        Guid userId, string conversationId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var id = ParseOwnedId(conversationId);
        var owned = await _conversations.Find(item => item.Id == id && item.UserId == userId)
            .AnyAsync(cancellationToken);
        if (!owned) throw new KeyNotFoundException("Không tìm thấy cuộc trò chuyện.");

        return await _messages.Find(item => item.ConversationId == id && item.UserId == userId)
            .SortBy(item => item.CreatedAt)
            .Limit(200)
            .Project(item => new AiChatMessageDto(
                item.Id.ToString(),
                item.Role,
                item.Content,
                item.CreatedAt,
                item.BookingDraft == null
                    ? null
                    : new AiBookingDraftDto(
                        new AiBookingLocationDto(
                            item.BookingDraft.Pickup.Address,
                            item.BookingDraft.Pickup.Latitude,
                            item.BookingDraft.Pickup.Longitude),
                        new AiBookingLocationDto(
                            item.BookingDraft.Destination.Address,
                            item.BookingDraft.Destination.Latitude,
                            item.BookingDraft.Destination.Longitude))))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteConversationAsync(
        Guid userId, string conversationId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var id = ParseOwnedId(conversationId);
        var deleted = await _conversations.DeleteOneAsync(
            item => item.Id == id && item.UserId == userId, cancellationToken);
        if (deleted.DeletedCount == 0) throw new KeyNotFoundException("Không tìm thấy cuộc trò chuyện.");
        await _messages.DeleteManyAsync(
            item => item.ConversationId == id && item.UserId == userId, cancellationToken);
        await _redis.RemoveAsync(ContextKey(userId, id));
    }

    private async Task<AiConversationDocument> ResolveConversationAsync(
        Guid userId, string? id, string content, DateTime now, DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var objectId = ParseOwnedId(id);
            var existing = await _conversations.Find(
                item => item.Id == objectId && item.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is null) throw new KeyNotFoundException("Không tìm thấy cuộc trò chuyện.");
            return existing;
        }

        var created = new AiConversationDocument
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            Title = content.Length <= 60 ? content : content[..60],
            CreatedAt = now,
            UpdatedAt = now,
            ExpiresAt = expiresAt
        };
        await _conversations.InsertOneAsync(created, cancellationToken: cancellationToken);
        return created;
    }

    private async Task<GeneratedReply> GenerateAsync(
        IReadOnlyList<CachedMessage> context,
        bool hasCurrentLocation,
        CancellationToken cancellationToken)
    {
        var locationInstruction = hasCurrentLocation
            ? """
              Ứng dụng đã cung cấp vị trí GPS hiện tại. Khi người dùng chỉ nói điểm đến,
              hãy để pickupAddress là null và không hỏi lại điểm đón; hệ thống sẽ tự dùng GPS.
              """
            : """
              Ứng dụng chưa cung cấp được vị trí GPS. Nếu thiếu điểm đón, hãy hỏi người dùng
              điểm đón cụ thể.
              """;
        var prompt = $$"""
            Bạn là trợ lý SafeRide và chỉ trả lời bằng tiếng Việt. Bạn hỗ trợ khách hàng sử dụng
            ứng dụng và chuẩn bị thông tin đặt chuyến, nhưng không tự tạo booking, không tự xác nhận
            giá và không khẳng định đã có tài xế.

            Hãy nhận biết các cách diễn đạt như "đặt từ A đến B", "đón ở A đi B" hoặc tương đương.
            Các câu rút gọn như "đến X", "đi X", "đặt xe đến X" luôn có X là điểm đến;
            không được hiểu X là điểm đón.
            Khi đã xác định được cả điểm đón và điểm đến:
            - Luôn điền pickupAddress bằng điểm đón và destinationAddress bằng điểm đến.
            - Giữ nguyên thông tin địa chỉ người dùng đã cung cấp, không hoán đổi hai địa điểm.
            - Trả lời rằng đã ghi nhận lộ trình và hướng dẫn người dùng kiểm tra rồi tiếp tục đặt
              chuyến. Không hỏi chung chung "Tôi có thể giúp gì tiếp theo?".

            Nếu thiếu điểm đón hoặc điểm đến, đặt trường còn thiếu thành null và hỏi đúng thông tin
            còn thiếu. Trả lời ngắn gọn, tự nhiên và không tuyên bố rằng chuyến xe đã được đặt.

            {{locationInstruction}}
            """;
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = prompt } } },
            contents = context.Select(message => new
            {
                role = message.Role == "assistant" ? "model" : "user",
                parts = new[] { new { text = message.Content } }
            }),
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        reply = new { type = "STRING" },
                        pickupAddress = new { type = "STRING", nullable = true },
                        destinationAddress = new { type = "STRING", nullable = true }
                    },
                    required = new[] { "reply", "pickupAddress", "destinationAddress" }
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(_options.GeminiModel)}:generateContent");
        request.Headers.Add("x-goog-api-key", _options.GeminiApiKey);
        request.Content = JsonContent.Create(body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var text = json.RootElement.GetProperty("candidates")[0]
            .GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        return JsonSerializer.Deserialize<GeneratedReply>(
            text ?? "", new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("AI trả về dữ liệu không hợp lệ.");
    }

    private async Task<BookingDraftResolution> ResolveBookingDraftAsync(
        GeneratedReply generated,
        AiCurrentLocationRequest? currentLocation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(generated.DestinationAddress))
            return new BookingDraftResolution(null, false, false, false);

        var useCurrentLocation = string.IsNullOrWhiteSpace(generated.PickupAddress)
            && IsValidCurrentLocation(currentLocation);
        var pickup = useCurrentLocation
            ? new MapPlaceDto
            {
                Name = string.IsNullOrWhiteSpace(currentLocation!.Address)
                    ? "Vị trí hiện tại"
                    : currentLocation.Address.Trim(),
                Address = currentLocation.Address?.Trim() ?? "Vị trí hiện tại",
                Lat = currentLocation.Latitude,
                Lng = currentLocation.Longitude
            }
            : await ResolvePlaceAsync(generated.PickupAddress ?? "", cancellationToken);
        var destination = await ResolvePlaceAsync(
            generated.DestinationAddress,
            cancellationToken,
            pickup?.Lat,
            pickup?.Lng);
        if (pickup is null || destination is null)
            return new BookingDraftResolution(
                null, pickup is not null, destination is not null, useCurrentLocation);

        return new BookingDraftResolution(
            new AiBookingDraftDto(
                new AiBookingLocationDto(
                    pickup.Address,
                    pickup.Lat,
                    pickup.Lng),
                new AiBookingLocationDto(
                    generated.DestinationAddress.Trim(),
                    destination.Lat,
                    destination.Lng)),
            true,
            true,
            useCurrentLocation);
    }

    private static bool IsValidCurrentLocation(AiCurrentLocationRequest? location) =>
        location is not null
        && double.IsFinite(location.Latitude)
        && double.IsFinite(location.Longitude)
        && location.Latitude is >= -90 and <= 90
        && location.Longitude is >= -180 and <= 180;

    private async Task<MapPlaceDto?> ResolvePlaceAsync(
        string query,
        CancellationToken cancellationToken,
        double? focusLat = null,
        double? focusLng = null)
    {
        var suggestions = await _geocoding.AutocompleteAsync(
            new MapAutocompleteRequest
            {
                Query = query,
                LocationLat = focusLat,
                LocationLng = focusLng
            },
            cancellationToken);
        var suggestion = focusLat.HasValue && focusLng.HasValue
            ? suggestions
                .Where(item => item.Lat.HasValue && item.Lng.HasValue)
                .OrderBy(item => DistanceSquared(
                    focusLat.Value,
                    focusLng.Value,
                    item.Lat!.Value,
                    item.Lng!.Value))
                .FirstOrDefault() ?? suggestions.FirstOrDefault()
            : suggestions.FirstOrDefault();

        if (suggestion is not null)
        {
            if (suggestion.Lat.HasValue && suggestion.Lng.HasValue)
            {
                return new MapPlaceDto
                {
                    ProviderPlaceId = suggestion.ProviderPlaceId,
                    Name = suggestion.PrimaryText,
                    Address = suggestion.SecondaryText,
                    Lat = suggestion.Lat.Value,
                    Lng = suggestion.Lng.Value
                };
            }

            if (!string.IsNullOrWhiteSpace(suggestion.ProviderPlaceId))
            {
                var detail = await _geocoding.GetPlaceDetailAsync(
                    suggestion.ProviderPlaceId,
                    cancellationToken);
                if (detail is not null) return detail;
            }
        }

        return (await _geocoding.GeocodeAsync(
            new MapGeocodeRequest { Query = query },
            cancellationToken)).FirstOrDefault();
    }

    private static double DistanceSquared(
        double fromLat,
        double fromLng,
        double toLat,
        double toLng)
    {
        var latitudeScale = Math.Cos(fromLat * Math.PI / 180d);
        var latDelta = toLat - fromLat;
        var lngDelta = (toLng - fromLng) * latitudeScale;
        return latDelta * latDelta + lngDelta * lngDelta;
    }

    private static string BuildGeocodingFailureReply(
        GeneratedReply generated,
        BookingDraftResolution resolution)
    {
        if (!resolution.PickupResolved && !resolution.DestinationResolved)
            return $"Mình chưa tìm thấy điểm đón “{generated.PickupAddress}” và điểm đến "
                + $"“{generated.DestinationAddress}”. Bạn vui lòng bổ sung phường, quận hoặc "
                + "tỉnh/thành phố cho cả hai địa điểm nhé.";

        if (!resolution.PickupResolved)
            return $"Mình chưa tìm thấy chính xác điểm đón “{generated.PickupAddress}”. "
                + "Bạn vui lòng bổ sung phường, quận hoặc tỉnh/thành phố nhé.";

        return $"Mình chưa tìm thấy chính xác điểm đến “{generated.DestinationAddress}”. "
            + "Bạn vui lòng bổ sung phường, quận hoặc tỉnh/thành phố nhé.";
    }

    private async Task<IReadOnlyList<CachedMessage>> GetContextAsync(
        Guid userId, ObjectId conversationId, CancellationToken cancellationToken)
    {
        var cached = await _redis.ListRangeAsync(
            ContextKey(userId, conversationId), 0, -1, cancellationToken);
        if (cached.Count > 0)
            return cached.Select(value => JsonSerializer.Deserialize<CachedMessage>(value)!)
                .Where(value => value is not null).ToList();

        var messages = await _messages.Find(
                item => item.ConversationId == conversationId && item.UserId == userId)
            .SortByDescending(item => item.CreatedAt)
            .Limit(_options.ContextMessageLimit)
            .ToListAsync(cancellationToken);
        return messages.OrderBy(item => item.CreatedAt)
            .Select(item => new CachedMessage(item.Role, item.Content)).ToList();
    }

    private Task CacheMessageAsync(
        Guid userId, ObjectId conversationId, string role, string content,
        CancellationToken cancellationToken) =>
        _redis.ListRightPushTrimAndExpireAsync(
            ContextKey(userId, conversationId),
            JsonSerializer.Serialize(new CachedMessage(role, content)),
            _options.ContextMessageLimit,
            TimeSpan.FromHours(_options.ContextTtlHours),
            cancellationToken);

    private static AiMessageDocument NewMessage(
        ObjectId conversationId, Guid userId, string role, string content,
        DateTime createdAt, DateTime expiresAt, AiBookingDraftDto? bookingDraft = null) => new()
    {
        Id = ObjectId.GenerateNewId(),
        ConversationId = conversationId,
        UserId = userId,
        Role = role,
        Content = content,
        BookingDraft = bookingDraft is null
            ? null
            : new AiBookingDraftDocument
            {
                Pickup = new AiBookingLocationDocument
                {
                    Address = bookingDraft.Pickup.Address,
                    Latitude = bookingDraft.Pickup.Latitude,
                    Longitude = bookingDraft.Pickup.Longitude
                },
                Destination = new AiBookingLocationDocument
                {
                    Address = bookingDraft.Destination.Address,
                    Latitude = bookingDraft.Destination.Latitude,
                    Longitude = bookingDraft.Destination.Longitude
                }
            },
        CreatedAt = createdAt,
        ExpiresAt = expiresAt
    };

    private static AiChatMessageDto Map(AiMessageDocument message) =>
        new(
            message.Id.ToString(),
            message.Role,
            message.Content,
            message.CreatedAt,
            message.BookingDraft is null
                ? null
                : new AiBookingDraftDto(
                    new AiBookingLocationDto(
                        message.BookingDraft.Pickup.Address,
                        message.BookingDraft.Pickup.Latitude,
                        message.BookingDraft.Pickup.Longitude),
                    new AiBookingLocationDto(
                        message.BookingDraft.Destination.Address,
                        message.BookingDraft.Destination.Latitude,
                        message.BookingDraft.Destination.Longitude)));

    private void EnsureEnabled()
    {
        if (!_options.Enabled ||
            string.IsNullOrWhiteSpace(_options.MongoConnectionString) ||
            string.IsNullOrWhiteSpace(_options.GeminiApiKey))
            throw new InvalidOperationException("Trợ lý AI chưa được cấu hình.");
    }

    private static ObjectId ParseOwnedId(string value) =>
        ObjectId.TryParse(value, out var id)
            ? id
            : throw new KeyNotFoundException("Không tìm thấy cuộc trò chuyện.");

    private static string ContextKey(Guid userId, ObjectId conversationId) =>
        $"ai-chat:context:{userId:N}:{conversationId}";

    private sealed record CachedMessage(string Role, string Content);
    private sealed record GeneratedReply(
        string Reply, string? PickupAddress, string? DestinationAddress);
    private sealed record BookingDraftResolution(
        AiBookingDraftDto? Draft,
        bool PickupResolved,
        bool DestinationResolved,
        bool UsedCurrentLocation);
}

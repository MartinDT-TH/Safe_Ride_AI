using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.AiChat;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.ExternalServices.Cloudinary;

namespace SafeRide.Infrastructure.AiChat;

public sealed class AiChatService : IAiChatService
{
    private readonly AiChatOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IRedisService _redis;
    private readonly IMapGeocodingService _geocoding;
    private readonly IMongoCollection<AiConversationDocument> _conversations;
    private readonly IMongoCollection<AiMessageDocument> _messages;
    private readonly ICloudinaryImageService _cloudinary;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IOptions<AiChatOptions> options,
        HttpClient httpClient,
        IRedisService redis,
        IMapGeocodingService geocoding,
        ICloudinaryImageService cloudinary,
        ILogger<AiChatService> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _redis = redis;
        _geocoding = geocoding;
        _cloudinary = cloudinary;
        _logger = logger;

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
        var languageCode = NormalizeLanguageCode(request.LanguageCode);
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
            context,
            IsValidCurrentLocation(request.CurrentLocation),
            languageCode,
            cancellationToken);
        var draftResolution = await ResolveBookingDraftAsync(
            generated, request.CurrentLocation, cancellationToken);
        var draft = draftResolution.Draft;
        if (draft is not null && draftResolution.UsedCurrentLocation)
        {
            generated = generated with
            {
                Reply = BuildCurrentLocationReply(
                    generated.DestinationAddress,
                    languageCode)
            };
        }
        if (draft is null &&
            !string.IsNullOrWhiteSpace(generated.DestinationAddress) &&
            (draftResolution.UsedCurrentLocation ||
             !string.IsNullOrWhiteSpace(generated.PickupAddress)))
        {
            generated = generated with
            {
                Reply = BuildGeocodingFailureReply(
                    generated,
                    draftResolution,
                    languageCode)
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

    public async Task<AiChatReplyDto> SendAudioAsync(
        Guid userId,
        Stream audio,
        string mimeType,
        string? conversationId,
        AiCurrentLocationRequest? currentLocation,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        if (!SupportedAudioTypes.Contains(mimeType))
            throw new ArgumentException("Định dạng ghi âm không được hỗ trợ.");

        using var buffer = new MemoryStream();
        await audio.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length is 0 || buffer.Length > _options.MaxAudioBytes)
            throw new ArgumentException("File ghi âm phải có dung lượng từ 1 byte đến 10 MB.");

        var bytes = buffer.ToArray();
        await using var uploadStream = new MemoryStream(bytes, writable: false);
        var extension = mimeType switch
        {
            "audio/aac" => ".aac",
            "audio/mpeg" => ".mp3",
            "audio/ogg" => ".ogg",
            "audio/wav" => ".wav",
            "audio/webm" => ".webm",
            _ => ".m4a"
        };
        var upload = await _cloudinary.UploadAiChatAudioAsync(
            userId,
            uploadStream,
            $"voice-{Guid.NewGuid():N}{extension}",
            cancellationToken);
        try
        {
            var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
            var transcript = await TranscribeAsync(
                bytes,
                mimeType,
                normalizedLanguageCode,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(transcript))
                throw new ArgumentException("Không nhận diện được nội dung trong file ghi âm.");

            var reply = await SendAsync(
                userId,
                new SendAiChatMessageRequest(
                    transcript,
                    conversationId,
                    currentLocation,
                    normalizedLanguageCode),
                cancellationToken);
            var messageId = ObjectId.Parse(reply.UserMessage.Id);
            await _messages.UpdateOneAsync(
                item => item.Id == messageId && item.UserId == userId,
                Builders<AiMessageDocument>.Update
                    .Set(item => item.IsAudio, true)
                    .Set(item => item.AudioUrl, upload.Url)
                    .Set(item => item.AudioPublicId, upload.PublicId)
                    .Set(item => item.AudioMimeType, mimeType)
                    .Set(item => item.AudioSizeBytes, buffer.Length),
                cancellationToken: cancellationToken);
            return reply with
            {
                UserMessage = reply.UserMessage with
                {
                    Content = VoiceMessageLabel(normalizedLanguageCode),
                    IsAudio = true,
                    AudioUrl = upload.Url,
                    AudioMimeType = mimeType,
                    AudioSizeBytes = buffer.Length
                }
            };
        }
        catch (Exception originalException)
        {
            try
            {
                await _cloudinary.DeleteAiChatAudioAsync(
                    upload.PublicId,
                    CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(
                    cleanupException,
                    "Could not roll back Cloudinary AI chat audio {PublicId} after {ExceptionType}.",
                    upload.PublicId,
                    originalException.GetType().Name);
            }
            throw;
        }
    }

    private async Task<string> TranscribeAsync(
        byte[] audio,
        string mimeType,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new
                        {
                            text = $"Transcribe this audio accurately. Return only the spoken text without explanation. "
                                + $"Prefer {LanguageName(languageCode)} writing when the speech permits it; do not translate the meaning."
                        },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = Convert.ToBase64String(audio)
                            }
                        }
                    }
                }
            }
        };
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(_options.GeminiModel)}:generateContent");
        request.Headers.Add("x-goog-api-key", _options.GeminiApiKey);
        request.Content = JsonContent.Create(body);
        using var response = await SendGeminiAsync(
            request, "transcription", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        return ExtractGeminiText(json.RootElement, "transcription");
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
                            item.BookingDraft.Destination.Longitude),
                        item.BookingDraft.VehicleQuery,
                        item.BookingDraft.PromotionCode,
                        item.BookingDraft.VehicleType,
                        item.BookingDraft.UseBestPromotion,
                        item.BookingDraft.AutoBook),
                item.IsAudio,
                item.AudioUrl,
                item.AudioMimeType,
                item.AudioSizeBytes))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteConversationAsync(
        Guid userId, string conversationId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var id = ParseOwnedId(conversationId);
        var owned = await _conversations.Find(
                item => item.Id == id && item.UserId == userId)
            .AnyAsync(cancellationToken);
        if (!owned) throw new KeyNotFoundException("Không tìm thấy cuộc trò chuyện.");
        var audioPublicIds = await _messages.Find(
                item => item.ConversationId == id &&
                        item.UserId == userId &&
                        item.AudioPublicId != null)
            .Project(item => item.AudioPublicId!)
            .ToListAsync(cancellationToken);
        foreach (var publicId in audioPublicIds)
            await _cloudinary.DeleteAiChatAudioAsync(publicId, cancellationToken);
        await _messages.DeleteManyAsync(
            item => item.ConversationId == id && item.UserId == userId, cancellationToken);
        await _conversations.DeleteOneAsync(
            item => item.Id == id && item.UserId == userId, cancellationToken);
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
        string languageCode,
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
            Bạn là trợ lý SafeRide. Luôn trả lời bằng {{LanguageName(languageCode)}} (mã {{languageCode}}),
            kể cả khi lịch sử hội thoại dùng ngôn ngữ khác. Bạn hỗ trợ khách hàng sử dụng
            ứng dụng, trích xuất chính xác ý định đặt chuyến để ứng dụng quyết định có tạo booking
            tự động hay không, và không khẳng định đã có tài xế.

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

            Dịch vụ xe máy hiện đã ngưng hoạt động. Với mọi yêu cầu đặt chuyến không nêu loại xe,
            luôn ưu tiên ô tô và đặt vehicleType="car". Nếu người dùng nhắc tên ô tô hoặc biển số
            ô tô, điền nguyên văn phần nhận diện đó vào vehicleQuery. Nếu người dùng yêu cầu ô tô/
            xe hơi, đặt vehicleType="car" và không điền vehicleQuery bằng tên loại phương tiện chung.
            Nếu người dùng yêu cầu xe máy, giải thích ngắn gọn rằng dịch vụ xe máy đang tạm ngưng,
            gợi ý đặt ô tô, đặt vehicleType="motorbike" và autoBook=false; không tự động tạo chuyến
            ô tô khi người dùng chưa đồng ý. Nếu người dùng nhắc mã giảm giá, điền mã vào promotionCode.
            Nếu họ yêu cầu voucher/mã giảm giá tối ưu, tốt nhất hoặc giảm nhiều nhất thì đặt
            useBestPromotion=true và không tự bịa promotionCode.

            Đặt autoBook=true khi người dùng ra lệnh rõ ràng đặt ô tô/tìm tài xế ngay và đã có đủ
            điểm đến (điểm đón có thể lấy từ GPS). Luôn đặt autoBook=false nếu họ yêu cầu xe máy,
            chỉ hỏi thông tin, đang cân nhắc, hoặc yêu cầu chưa đủ rõ. Khi autoBook=true, trả lời rằng
            hệ thống đang chuẩn bị đặt chuyến và tìm tài xế; không nói rằng đã tìm thấy tài xế.

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
                        destinationAddress = new { type = "STRING", nullable = true },
                        vehicleQuery = new { type = "STRING", nullable = true },
                        promotionCode = new { type = "STRING", nullable = true },
                        vehicleType = new { type = "STRING", nullable = true },
                        useBestPromotion = new { type = "BOOLEAN" },
                        autoBook = new { type = "BOOLEAN" }
                    },
                    required = new[]
                    {
                        "reply", "pickupAddress", "destinationAddress", "vehicleQuery",
                        "promotionCode", "vehicleType", "useBestPromotion", "autoBook"
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(_options.GeminiModel)}:generateContent");
        request.Headers.Add("x-goog-api-key", _options.GeminiApiKey);
        request.Content = JsonContent.Create(body);
        using var response = await SendGeminiAsync(
            request, "booking_extraction", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var text = ExtractGeminiText(json.RootElement, "booking_extraction");
        return JsonSerializer.Deserialize<GeneratedReply>(
            text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("AI trả về dữ liệu không hợp lệ.");
    }

    private string ExtractGeminiText(JsonElement root, string stage)
    {
        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var candidate = candidates[0];
            if (candidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textElement) &&
                        textElement.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(textElement.GetString()))
                        return textElement.GetString()!.Trim();
                }
            }

            var finishReason = candidate.TryGetProperty("finishReason", out var reason)
                ? reason.GetString()
                : null;
            _logger.LogWarning(
                "Gemini returned no text during {Stage}. Finish reason: {FinishReason}.",
                stage,
                finishReason ?? "unknown");
        }
        else
        {
            var promptFeedback = root.TryGetProperty("promptFeedback", out var feedback)
                ? feedback.GetRawText()
                : "not provided";
            _logger.LogWarning(
                "Gemini returned no candidates during {Stage}. Prompt feedback: {PromptFeedback}.",
                stage,
                promptFeedback);
        }

        throw new InvalidOperationException(
            stage == "transcription"
                ? "Gemini không thể phiên âm file ghi âm này."
                : "Gemini không trả về nội dung có thể xử lý.");
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
                    destination.Lng),
                NormalizeHint(generated.VehicleQuery),
                NormalizeHint(generated.PromotionCode),
                NormalizeVehicleType(generated.VehicleType),
                generated.UseBestPromotion,
                generated.AutoBook),
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
        BookingDraftResolution resolution,
        string languageCode)
    {
        var pickup = generated.PickupAddress;
        var destination = generated.DestinationAddress;
        if (languageCode == "en")
        {
            if (!resolution.PickupResolved && !resolution.DestinationResolved)
                return $"I couldn't find pickup “{pickup}” or destination “{destination}”. Add the district, city, or province for both locations.";
            if (!resolution.PickupResolved)
                return $"I couldn't find the exact pickup “{pickup}”. Add the district, city, or province.";
            return $"I couldn't find the exact destination “{destination}”. Add the district, city, or province.";
        }
        if (languageCode == "ko")
        {
            if (!resolution.PickupResolved && !resolution.DestinationResolved)
                return $"출발지 ‘{pickup}’와 목적지 ‘{destination}’를 찾지 못했습니다. 두 위치의 구·시·도를 추가해 주세요.";
            if (!resolution.PickupResolved)
                return $"출발지 ‘{pickup}’를 정확히 찾지 못했습니다. 구·시·도를 추가해 주세요.";
            return $"목적지 ‘{destination}’를 정확히 찾지 못했습니다. 구·시·도를 추가해 주세요.";
        }
        if (languageCode == "ja")
        {
            if (!resolution.PickupResolved && !resolution.DestinationResolved)
                return $"乗車地「{pickup}」と目的地「{destination}」が見つかりません。両方の区、市、都道府県を追加してください。";
            if (!resolution.PickupResolved)
                return $"乗車地「{pickup}」を正確に特定できません。区、市、都道府県を追加してください。";
            return $"目的地「{destination}」を正確に特定できません。区、市、都道府県を追加してください。";
        }
        if (languageCode == "zh")
        {
            if (!resolution.PickupResolved && !resolution.DestinationResolved)
                return $"未找到上车点“{pickup}”和目的地“{destination}”。请补充两个地点的区、市或省信息。";
            if (!resolution.PickupResolved)
                return $"未能准确找到上车点“{pickup}”。请补充区、市或省信息。";
            return $"未能准确找到目的地“{destination}”。请补充区、市或省信息。";
        }

        if (!resolution.PickupResolved && !resolution.DestinationResolved)
            return $"Mình chưa tìm thấy điểm đón “{pickup}” và điểm đến "
                + $"“{destination}”. Bạn vui lòng bổ sung phường, quận hoặc "
                + "tỉnh/thành phố cho cả hai địa điểm nhé.";

        if (!resolution.PickupResolved)
            return $"Mình chưa tìm thấy chính xác điểm đón “{pickup}”. "
                + "Bạn vui lòng bổ sung phường, quận hoặc tỉnh/thành phố nhé.";

        return $"Mình chưa tìm thấy chính xác điểm đến “{destination}”. "
            + "Bạn vui lòng bổ sung phường, quận hoặc tỉnh/thành phố nhé.";
    }

    private static string BuildCurrentLocationReply(
        string? destination,
        string languageCode) => languageCode switch
        {
            "en" => $"I used your current location as the pickup and noted “{destination}” as the destination. Check the route, then continue booking.",
            "ko" => $"현재 위치를 출발지로 사용하고 ‘{destination}’를 목적지로 기록했습니다. 경로를 확인한 뒤 예약을 계속해 주세요.",
            "ja" => $"現在地を乗車地として使用し、「{destination}」を目的地として記録しました。ルートを確認して予約を続けてください。",
            "zh" => $"已将您的当前位置设为上车点，并记录目的地“{destination}”。请确认路线后继续预订。",
            _ => $"Mình đã dùng vị trí hiện tại làm điểm đón và ghi nhận điểm đến “{destination}”. Bạn hãy kiểm tra lộ trình rồi tiếp tục đặt chuyến nhé."
        };

    private static string VoiceMessageLabel(string languageCode) =>
        languageCode switch
        {
            "en" => "Voice message",
            "ko" => "음성 메시지",
            "ja" => "音声メッセージ",
            "zh" => "语音消息",
            _ => "Tin nhắn thoại"
        };

    private static string NormalizeLanguageCode(string? languageCode) =>
        languageCode?.Trim().ToLowerInvariant() switch
        {
            "en" => "en",
            "ko" => "ko",
            "ja" => "ja",
            "zh" or "zh-cn" or "zh-hans" => "zh",
            _ => "vi"
        };

    private static string LanguageName(string languageCode) =>
        languageCode switch
        {
            "en" => "English",
            "ko" => "Korean",
            "ja" => "Japanese",
            "zh" => "Simplified Chinese",
            _ => "Vietnamese"
        };

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
                },
                VehicleQuery = bookingDraft.VehicleQuery,
                PromotionCode = bookingDraft.PromotionCode,
                VehicleType = bookingDraft.VehicleType,
                UseBestPromotion = bookingDraft.UseBestPromotion,
                AutoBook = bookingDraft.AutoBook
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
                        message.BookingDraft.Destination.Longitude),
                    message.BookingDraft.VehicleQuery,
                    message.BookingDraft.PromotionCode,
                    message.BookingDraft.VehicleType,
                    message.BookingDraft.UseBestPromotion,
                    message.BookingDraft.AutoBook),
            message.IsAudio,
            message.AudioUrl,
            message.AudioMimeType,
            message.AudioSizeBytes);

    private static string? NormalizeHint(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeVehicleType(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "car" => "car",
            "motorbike" => "motorbike",
            _ => null
        };

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

    private async Task<HttpResponseMessage> SendGeminiAsync(
        HttpRequestMessage request,
        string stage,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var attemptRequest = await CloneRequestAsync(request, cancellationToken);
            var response = await _httpClient.SendAsync(attemptRequest, cancellationToken);
            var retryable = response.StatusCode is
                System.Net.HttpStatusCode.ServiceUnavailable or
                System.Net.HttpStatusCode.TooManyRequests;
            if (!retryable || attempt == maxAttempts)
                return response;

            _logger.LogWarning(
                "Gemini returned {StatusCode} during {Stage} with model {Model}; retry {Attempt}/{MaxAttempts}.",
                (int)response.StatusCode,
                stage,
                _options.GeminiModel,
                attempt,
                maxAttempts);
            var retryAfter = response.Headers.RetryAfter?.Delta;
            response.Dispose();
            var delay = retryAfter is { } providerDelay &&
                        providerDelay <= TimeSpan.FromSeconds(10)
                ? providerDelay
                : TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
            await Task.Delay(delay, cancellationToken);
        }

        throw new InvalidOperationException("Gemini retry loop ended unexpectedly.");
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }

    private static readonly HashSet<string> SupportedAudioTypes =
    [
        "audio/aac", "audio/m4a", "audio/mp4", "audio/mpeg", "audio/ogg", "audio/wav",
        "audio/webm"
    ];

    private sealed record CachedMessage(string Role, string Content);
    private sealed record GeneratedReply(
        string Reply,
        string? PickupAddress,
        string? DestinationAddress,
        string? VehicleQuery,
        string? PromotionCode,
        string? VehicleType,
        bool UseBestPromotion,
        bool AutoBook);
    private sealed record BookingDraftResolution(
        AiBookingDraftDto? Draft,
        bool PickupResolved,
        bool DestinationResolved,
        bool UsedCurrentLocation);
}

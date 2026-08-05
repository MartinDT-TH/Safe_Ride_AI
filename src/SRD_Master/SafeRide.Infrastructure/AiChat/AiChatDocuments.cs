using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SafeRide.Infrastructure.AiChat;

internal sealed class AiConversationDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal sealed class AiMessageDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    public ObjectId ConversationId { get; set; }
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid UserId { get; set; }
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public AiBookingDraftDocument? BookingDraft { get; set; }
    public bool IsAudio { get; set; }
    public string? AudioUrl { get; set; }
    public string? AudioPublicId { get; set; }
    public string? AudioMimeType { get; set; }
    public long? AudioSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal sealed class AiBookingDraftDocument
{
    public AiBookingLocationDocument Pickup { get; set; } = new();
    public AiBookingLocationDocument Destination { get; set; } = new();
    public string? VehicleQuery { get; set; }
    public string? PromotionCode { get; set; }
    public string? VehicleType { get; set; }
    public bool UseBestPromotion { get; set; }
    public bool AutoBook { get; set; }
}

internal sealed class AiBookingLocationDocument
{
    public string Address { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

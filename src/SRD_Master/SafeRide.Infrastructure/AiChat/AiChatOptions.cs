using System.ComponentModel.DataAnnotations;

namespace SafeRide.Infrastructure.AiChat;

public sealed class AiChatOptions
{
    public const string SectionName = "AiChat";

    public bool Enabled { get; init; }
    public string MongoConnectionString { get; set; } = "";
    public string MongoDatabase { get; init; } = "saferide_ai";
    public string GeminiApiKey { get; init; } = "";
    public string GeminiModel { get; init; } = "gemini-2.5-flash";
    public bool TripChatTranslationEnabled { get; init; }
    public int TripChatTranslationTimeoutSeconds { get; init; } = 10;
    public int TripChatTranslationMaxRetries { get; init; } = 1;
    public int RetentionDays { get; init; } = 90;
    public int ContextMessageLimit { get; init; } = 20;
    public int ContextTtlHours { get; init; } = 24;
    public int MaxMessageLength { get; init; } = 1000;
    public int MaxAudioBytes { get; init; } = 10 * 1024 * 1024;
}

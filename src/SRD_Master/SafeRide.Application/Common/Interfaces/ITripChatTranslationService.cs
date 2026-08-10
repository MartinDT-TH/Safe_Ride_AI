namespace SafeRide.Application.Common.Interfaces;

public interface ITripChatTranslationService
{
    Task<TripChatTranslation?> TranslateAsync(
        string message,
        CancellationToken cancellationToken = default);
}

public sealed record TripChatTranslation(
    string SourceLanguage,
    IReadOnlyDictionary<string, string> Translations);

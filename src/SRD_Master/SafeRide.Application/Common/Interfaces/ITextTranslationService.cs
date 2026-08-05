namespace SafeRide.Application.Common.Interfaces;

public interface ITextTranslationService
{
    Task<IReadOnlyDictionary<string, LocalizedText>> TranslateFromVietnameseAsync(
        string title,
        string content,
        CancellationToken cancellationToken = default);
}

public sealed record LocalizedText(string Title, string Content);

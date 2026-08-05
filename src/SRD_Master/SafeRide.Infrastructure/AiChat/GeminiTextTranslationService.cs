using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.AiChat;

public sealed class GeminiTextTranslationService : ITextTranslationService
{
    private static readonly string[] RequiredLocales = ["en", "ko", "ja", "zh"];

    private readonly HttpClient _httpClient;
    private readonly AiChatOptions _options;

    public GeminiTextTranslationService(
        HttpClient httpClient,
        IOptions<AiChatOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyDictionary<string, LocalizedText>> TranslateFromVietnameseAsync(
        string title,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.GeminiApiKey))
            throw new InvalidOperationException("Gemini API key is not configured for notification translation.");

        var prompt = $$$"""
            Translate this Vietnamese mobile notification into English (en), Korean (ko),
            Japanese (ja), and Simplified Chinese (zh). Preserve meaning, names, numbers,
            promotion codes, currency, and line breaks. Keep each title at most 40 characters
            and each content at most 140 characters. Return only valid JSON with this shape:
            {"en":{"title":"...","content":"..."},"ko":{...},"ja":{...},"zh":{...}}

            Vietnamese title: {{{title}}}
            Vietnamese content: {{{content}}}
            """;

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json"
            }
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(_options.GeminiModel)}:generateContent");
        request.Headers.Add("x-goog-api-key", _options.GeminiApiKey);
        request.Content = JsonContent.Create(requestBody);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var json = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Gemini returned an empty notification translation.");

        var translations = JsonSerializer.Deserialize<Dictionary<string, LocalizedText>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Gemini returned invalid notification translations.");

        foreach (var locale in RequiredLocales)
        {
            if (!translations.TryGetValue(locale, out var value) ||
                string.IsNullOrWhiteSpace(value.Title) ||
                string.IsNullOrWhiteSpace(value.Content))
                throw new InvalidOperationException($"Gemini did not return a valid {locale} translation.");
        }

        return translations;
    }
}

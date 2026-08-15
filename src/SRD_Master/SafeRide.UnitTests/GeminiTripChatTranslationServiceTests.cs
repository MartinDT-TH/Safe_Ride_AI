using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SafeRide.Infrastructure.AiChat;
using SafeRide.Infrastructure.TripChat;

namespace SafeRide.UnitTests;

public sealed class GeminiTripChatTranslationServiceTests
{
    [Fact]
    public async Task TranslateAsync_AfterTransientFailure_RetriesAndReturnsPartialTranslations()
    {
        var handler = new SequenceHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            CreateGeminiResponse(new
            {
                sourceLanguage = "vi",
                translations = new { en = "Okay, my friend." }
            }));
        var service = CreateService(handler, enabled: true, maxRetries: 1);

        var result = await service.TranslateAsync("Được bạn");

        Assert.NotNull(result);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal("vi", result.SourceLanguage);
        Assert.Equal("Okay, my friend.", result.Translations["en"]);
    }

    [Fact]
    public async Task TranslateAsync_WhenDisabled_DoesNotCallGemini()
    {
        var handler = new SequenceHttpMessageHandler();
        var service = CreateService(handler, enabled: false, maxRetries: 1);

        var result = await service.TranslateAsync("Xin chào");

        Assert.Null(result);
        Assert.Equal(0, handler.RequestCount);
    }

    private static GeminiTripChatTranslationService CreateService(
        HttpMessageHandler handler,
        bool enabled,
        int maxRetries)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        var options = Options.Create(new AiChatOptions
        {
            GeminiApiKey = "test-key",
            GeminiModel = "test-model",
            TripChatTranslationEnabled = enabled,
            TripChatTranslationTimeoutSeconds = 10,
            TripChatTranslationMaxRetries = maxRetries
        });
        return new GeminiTripChatTranslationService(
            client,
            options,
            NullLogger<GeminiTripChatTranslationService>.Instance);
    }

    private static HttpResponseMessage CreateGeminiResponse(object translation)
    {
        var response = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = JsonSerializer.Serialize(translation) }
                        }
                    }
                }
            }
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(response),
                Encoding.UTF8,
                "application/json")
        };
    }

    private sealed class SequenceHttpMessageHandler(
        params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No HTTP response was configured for this test.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}

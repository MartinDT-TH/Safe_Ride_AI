using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace SafeRide.Infrastructure.AiChat;

internal sealed class AiChatMongoInitializer(
    IOptions<AiChatOptions> options,
    ILogger<AiChatMongoInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("AI chat MongoDB initialization skipped because AiChat is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.MongoConnectionString))
        {
            logger.LogWarning(
                "AI chat MongoDB initialization skipped because ConnectionStrings:MongoDB is empty.");
            return;
        }

        try
        {
            var database = new MongoClient(settings.MongoConnectionString)
                .GetDatabase(settings.MongoDatabase);
            var conversations =
                database.GetCollection<AiConversationDocument>("conversations");
            var messages = database.GetCollection<AiMessageDocument>("messages");

            await conversations.Indexes.CreateManyAsync([
                new CreateIndexModel<AiConversationDocument>(
                    Builders<AiConversationDocument>.IndexKeys
                        .Ascending(item => item.UserId)
                        .Descending(item => item.UpdatedAt)),
                new CreateIndexModel<AiConversationDocument>(
                    Builders<AiConversationDocument>.IndexKeys
                        .Ascending(item => item.ExpiresAt),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
            ], cancellationToken);

            await messages.Indexes.CreateManyAsync([
                new CreateIndexModel<AiMessageDocument>(
                    Builders<AiMessageDocument>.IndexKeys
                        .Ascending(item => item.ConversationId)
                        .Ascending(item => item.CreatedAt)),
                new CreateIndexModel<AiMessageDocument>(
                    Builders<AiMessageDocument>.IndexKeys
                        .Ascending(item => item.ExpiresAt),
                    new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
            ], cancellationToken);

            logger.LogInformation(
                "AI chat MongoDB collections and indexes initialized in database {Database}.",
                settings.MongoDatabase);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not initialize AI chat MongoDB database {Database}.",
                settings.MongoDatabase);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

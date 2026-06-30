using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.Authentication;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class CleanupExpiredRefreshTokensJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IDateTimeProvider _clock;
    private readonly IOptions<RefreshTokenCleanupOptions> _options;
    private readonly ILogger<CleanupExpiredRefreshTokensJob> _logger;

    public CleanupExpiredRefreshTokensJob(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IDateTimeProvider clock,
        IOptions<RefreshTokenCleanupOptions> options,
        ILogger<CleanupExpiredRefreshTokensJob> logger)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var retentionDays = Math.Max(0, _options.Value.CleanupRetentionDays);
        var cutoff = _clock.UtcNow.AddDays(-retentionDays);
        var batchSize = Math.Max(1, _options.Value.CleanupBatchSize);
        var totalDeleted = 0;

        while (true)
        {
            var expiredTokens = await _dbContext.RefreshTokens
                .Where(token => token.ExpiresAt <= cutoff)
                .OrderBy(token => token.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (expiredTokens.Count == 0)
            {
                break;
            }

            foreach (var token in expiredTokens)
            {
                await TryRemoveCacheAsync(token.TokenHash);
            }

            _dbContext.RefreshTokens.RemoveRange(expiredTokens);
            await _dbContext.SaveChangesAsync(cancellationToken);
            totalDeleted += expiredTokens.Count;

            if (expiredTokens.Count < batchSize)
            {
                break;
            }
        }

        if (totalDeleted == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Deleted {TokenCount} refresh tokens expired before {CutoffUtc}.",
            totalDeleted,
            cutoff);
    }

    private async Task TryRemoveCacheAsync(byte[] tokenHash)
    {
        try
        {
            await _redisService.RemoveAsync(
                RedisKeys.RefreshToken(Convert.ToHexString(tokenHash)));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Redis unavailable while removing expired refresh token cache.");
        }
    }
}

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

        var expiredTokens = await _dbContext.RefreshTokens
            .Where(token => token.ExpiresAt <= cutoff)
            .Select(token => new
            {
                token.Id,
                token.TokenHash
            })
            .ToListAsync(cancellationToken);

        if (expiredTokens.Count == 0)
        {
            return;
        }

        foreach (var token in expiredTokens)
        {
            await TryRemoveCacheAsync(token.TokenHash);
        }

        var tokenIds = expiredTokens.Select(token => token.Id).ToList();
        var tokensToDelete = await _dbContext.RefreshTokens
            .Where(token => tokenIds.Contains(token.Id))
            .ToListAsync(cancellationToken);

        _dbContext.RefreshTokens.RemoveRange(tokensToDelete);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted {TokenCount} refresh tokens expired before {CutoffUtc}.",
            tokensToDelete.Count,
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

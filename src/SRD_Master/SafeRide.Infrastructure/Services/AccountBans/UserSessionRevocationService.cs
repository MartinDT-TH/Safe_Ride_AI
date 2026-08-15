using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Services.AccountBans;

public sealed class UserSessionRevocationService : IUserSessionRevocationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly ILogger<UserSessionRevocationService> _logger;

    public UserSessionRevocationService(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        ILogger<UserSessionRevocationService> logger)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _logger = logger;
    }

    public async Task RevokeAllUserSessionsAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var activeTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = utcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            try
            {
                await _redisService.RemoveAsync(
                    RedisKeys.RefreshToken(Convert.ToHexString(token.TokenHash)));
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not remove refresh-token cache for banned user {UserId}. Reason={Reason}",
                    userId,
                    reason);
            }
        }
    }
}

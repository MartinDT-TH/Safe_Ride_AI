using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.AccountBans;
using SafeRide.Application.Features.AccountBans.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Data;

namespace SafeRide.Infrastructure.Services.AccountBans;

public sealed class AccountBanService :
    IAccountBanManagementService,
    IAccountBanEvaluationService,
    IAccountRestrictionService
{
    private const string AutomaticTriggerPrefix = "rating";

    private sealed record AccountRestrictionNotification(
        Guid UserId,
        AccountBanType BanType,
        string Reason,
        string Message,
        DateTime UtcNow,
        DateTime? EndsAt);

    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUserSessionRevocationService _sessionRevocationService;
    private readonly IAccountRestrictionRealtimeService _realtimeService;
    private readonly IRedisService _redisService;
    private readonly ILogger<AccountBanService> _logger;

    public AccountBanService(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IUserSessionRevocationService sessionRevocationService,
        IAccountRestrictionRealtimeService realtimeService,
        IRedisService redisService,
        ILogger<AccountBanService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _sessionRevocationService = sessionRevocationService;
        _realtimeService = realtimeService;
        _redisService = redisService;
        _logger = logger;
    }

    public async Task<AccountBanConfigurationResponse> GetConfigurationAsync(
        CancellationToken cancellationToken)
    {
        var configuration = await GetConfigurationEntityAsync(cancellationToken);
        return ToResponse(configuration);
    }

    public async Task<AccountBanConfigurationResponse> UpdateConfigurationAsync(
        int negativeFeedbackThreshold,
        int negativeRatingMaxScore,
        int temporaryBanDurationDays,
        int maximumTemporaryBans,
        bool isEnabled,
        Guid updatedByUserId,
        CancellationToken cancellationToken)
    {
        ValidateConfiguration(
            negativeFeedbackThreshold,
            negativeRatingMaxScore,
            temporaryBanDurationDays,
            maximumTemporaryBans);

        var configuration = await GetConfigurationEntityAsync(cancellationToken);
        var utcNow = _dateTimeProvider.UtcNow;

        configuration.NegativeFeedbackThreshold = negativeFeedbackThreshold;
        configuration.NegativeRatingMaxScore = negativeRatingMaxScore;
        configuration.TemporaryBanDurationDays = temporaryBanDurationDays;
        configuration.MaximumTemporaryBans = maximumTemporaryBans;
        configuration.IsEnabled = isEnabled;
        configuration.UpdatedAt = utcNow;
        configuration.UpdatedByUserId = updatedByUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(configuration);
    }

    public async Task EvaluateRatingAsync(
        long ratingId,
        CancellationToken cancellationToken)
    {
        var rating = await _dbContext.Ratings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ratingId, cancellationToken);
        if (rating is null)
        {
            return;
        }

        var configuration = await GetConfigurationEntityAsync(cancellationToken);
        if (!configuration.IsEnabled ||
            rating.RatingScore > configuration.NegativeRatingMaxScore)
        {
            return;
        }

        var alreadyProcessed = await _dbContext.AccountBanHistories
            .AsNoTracking()
            .AnyAsync(
                x => x.TriggeringRatingId == rating.Id &&
                    x.Source == AccountBanSource.AutomaticNegativeFeedback,
                cancellationToken);
        if (alreadyProcessed)
        {
            return;
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var restriction = await strategy.ExecuteAsync(async () =>
        {
            // A retrying SQL Server strategy must own the complete user transaction.
            // Otherwise the first query executed after BeginTransactionAsync throws.
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(x => x.Id == rating.DriverId, cancellationToken);
            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var utcNow = _dateTimeProvider.UtcNow;
            await ExpireTemporaryBansAsync(user, utcNow, cancellationToken);

            var activeAutomaticRestriction = await _dbContext.AccountBanHistories
                .AsNoTracking()
                .AnyAsync(
                    x => x.UserId == user.Id &&
                        x.Source == AccountBanSource.AutomaticNegativeFeedback &&
                        x.Status == AccountBanStatus.Active &&
                        (x.EndsAt == null || x.EndsAt > utcNow),
                    cancellationToken);
            if (activeAutomaticRestriction)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var lastAutomaticBanCreatedAt = await _dbContext.AccountBanHistories
                .AsNoTracking()
                .Where(x =>
                    x.UserId == user.Id &&
                    x.Source == AccountBanSource.AutomaticNegativeFeedback)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var negativeFeedbackCount = await _dbContext.Ratings
                .AsNoTracking()
                .CountAsync(
                    x => x.DriverId == user.Id &&
                        x.RatingScore <= configuration.NegativeRatingMaxScore &&
                        (lastAutomaticBanCreatedAt == null ||
                            x.CreatedAt > lastAutomaticBanCreatedAt),
                    cancellationToken);
            if (negativeFeedbackCount < configuration.NegativeFeedbackThreshold)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var previousTemporaryBanCount = await _dbContext.AccountBanHistories
                .AsNoTracking()
                .CountAsync(
                    x => x.UserId == user.Id &&
                        x.Source == AccountBanSource.AutomaticNegativeFeedback &&
                        x.BanType == AccountBanType.Temporary,
                    cancellationToken);
            var shouldPermanentBan =
                previousTemporaryBanCount >= configuration.MaximumTemporaryBans;
            var banType = shouldPermanentBan
                ? AccountBanType.Permanent
                : AccountBanType.Temporary;
            int? temporarySequence = shouldPermanentBan
                ? null
                : previousTemporaryBanCount + 1;
            DateTime? endsAt = shouldPermanentBan
                ? null
                : utcNow.AddDays(configuration.TemporaryBanDurationDays);
            var reason = BuildAutomaticBanReason(
                banType,
                configuration,
                negativeFeedbackCount);
            var message = BuildUserBanMessage(
                banType,
                reason,
                utcNow,
                endsAt);

            _dbContext.AccountBanHistories.Add(new AccountBanHistory
            {
                UserId = user.Id,
                BanType = banType,
                Source = AccountBanSource.AutomaticNegativeFeedback,
                Status = AccountBanStatus.Active,
                Reason = reason,
                Trigger = $"{AutomaticTriggerPrefix}:{rating.Id}",
                StartedAt = utcNow,
                EndsAt = endsAt,
                CreatedAt = utcNow,
                TriggeringRatingId = rating.Id,
                NegativeFeedbackCount = negativeFeedbackCount,
                TemporaryBanSequence = temporarySequence
            });

            user.IsActive = false;
            user.BanReason = reason;
            user.UpdatedAt = utcNow;

            await SetDriverOfflineIfNeededAsync(user.Id, utcNow, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _sessionRevocationService.RevokeAllUserSessionsAsync(
                user.Id,
                reason,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AccountRestrictionNotification(
                user.Id,
                banType,
                reason,
                message,
                utcNow,
                endsAt);
        });

        if (restriction is not null)
        {
            await PublishAccountRestrictionAsync(
                restriction.UserId,
                restriction.BanType,
                restriction.Reason,
                restriction.Message,
                restriction.UtcNow,
                restriction.EndsAt,
                cancellationToken);
        }
    }

    public async Task<AccountRestrictionCheckResult> CheckAccountAccessAsync(
        Guid userId,
        bool releaseExpiredTemporaryBans,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return AccountRestrictionCheckResult.Denied(
                "auth.account_not_found",
                "Không tìm thấy tài khoản.");
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var expiredTemporaryBans = releaseExpiredTemporaryBans
            ? await ExpireTemporaryBansAsync(user, utcNow, cancellationToken)
            : [];

        var activeRestrictions = await _dbContext.AccountBanHistories
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.Status == AccountBanStatus.Active &&
                (x.EndsAt == null || x.EndsAt > utcNow))
            .ToListAsync(cancellationToken);
        var activeRestriction = activeRestrictions
            .OrderByDescending(GetRestrictionPriority)
            .ThenByDescending(x => x.StartedAt)
            .FirstOrDefault();

        if (activeRestriction is null)
        {
            if (expiredTemporaryBans.Count > 0 &&
                !user.IsActive &&
                expiredTemporaryBans.Any(x =>
                    string.Equals(user.BanReason, x.Reason, StringComparison.Ordinal)))
            {
                user.IsActive = true;
                user.BanReason = null;
                user.UpdatedAt = utcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (user.IsActive)
            {
                return AccountRestrictionCheckResult.Allowed();
            }

            return AccountRestrictionCheckResult.Denied(
                "auth.account_inactive",
                string.IsNullOrWhiteSpace(user.BanReason)
                    ? "Tài khoản đã bị vô hiệu hóa."
                    : user.BanReason);
        }

        return AccountRestrictionCheckResult.Denied(
            GetAuthCode(activeRestriction),
            BuildUserBanMessage(
                activeRestriction.BanType,
                activeRestriction.Reason,
                utcNow,
                activeRestriction.EndsAt),
            GetRetryAfterSeconds(activeRestriction, utcNow));
    }

    public async Task RecordManualLockAsync(
        Guid userId,
        Guid adminUserId,
        string reason,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var activeManualLock = await _dbContext.AccountBanHistories
            .FirstOrDefaultAsync(
                x => x.UserId == userId &&
                    x.Source == AccountBanSource.ManualAdmin &&
                    x.BanType == AccountBanType.ManualLock &&
                    x.Status == AccountBanStatus.Active,
                cancellationToken);

        if (activeManualLock is null)
        {
            _dbContext.AccountBanHistories.Add(new AccountBanHistory
            {
                UserId = userId,
                BanType = AccountBanType.ManualLock,
                Source = AccountBanSource.ManualAdmin,
                Status = AccountBanStatus.Active,
                Reason = reason,
                Trigger = "admin:block",
                StartedAt = utcNow,
                CreatedAt = utcNow,
                CreatedByUserId = adminUserId
            });
        }
        else
        {
            activeManualLock.Reason = reason;
            activeManualLock.CreatedByUserId = adminUserId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordManualUnlockAsync(
        Guid userId,
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var activeManualLocks = await _dbContext.AccountBanHistories
            .Where(x =>
                x.UserId == userId &&
                x.Source == AccountBanSource.ManualAdmin &&
                x.BanType == AccountBanType.ManualLock &&
                x.Status == AccountBanStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var manualLock in activeManualLocks)
        {
            manualLock.Status = AccountBanStatus.Released;
            manualLock.ReleasedAt = utcNow;
            manualLock.ReleasedByUserId = adminUserId;
            manualLock.ReleaseReason = "Mở khóa bởi quản trị viên";
        }

        if (activeManualLocks.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<AccountBanConfiguration> GetConfigurationEntityAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.AccountBanConfigurations
            .FirstOrDefaultAsync(
                x => x.Id == AccountBanConfiguration.SingletonId,
                cancellationToken)
            ?? throw new AccountBanException(
                "account_ban.configuration_missing",
                "Chưa có cấu hình khóa tài khoản tự động.",
                StatusCodes.Status500InternalServerError);
    }

    private async Task<List<AccountBanHistory>> ExpireTemporaryBansAsync(
        AspNetUser user,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var expiredTemporaryBans = await _dbContext.AccountBanHistories
            .Where(x =>
                x.UserId == user.Id &&
                x.BanType == AccountBanType.Temporary &&
                x.Status == AccountBanStatus.Active &&
                x.EndsAt <= utcNow)
            .ToListAsync(cancellationToken);

        foreach (var ban in expiredTemporaryBans)
        {
            ban.Status = AccountBanStatus.Expired;
            ban.ReleasedAt = ban.EndsAt;
            ban.ReleaseReason = "Khóa tạm thời đã hết hạn";
        }

        if (expiredTemporaryBans.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return expiredTemporaryBans;
    }

    private async Task SetDriverOfflineIfNeededAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(x => x.DriverId == userId, cancellationToken);
        if (profile is null)
        {
            return;
        }

        profile.WorkStatus = DriverWorkStatus.Offline;
        profile.UpdatedAt = utcNow;

        try
        {
            await _redisService.GeoRemoveAsync(
                RedisKeys.OnlineDriversGeo,
                userId.ToString(),
                cancellationToken);
            await _redisService.RemoveAsync(RedisKeys.DriverOnline(userId));
            await _redisService.RemoveAsync(RedisKeys.DriverStatus(userId));
            await _redisService.RemoveAsync(RedisKeys.DriverLocation(userId));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not clear realtime driver presence for banned user {UserId}.",
                userId);
        }
    }

    private async Task PublishAccountRestrictionAsync(
        Guid userId,
        AccountBanType banType,
        string reason,
        string message,
        DateTime startedAt,
        DateTime? endsAt,
        CancellationToken cancellationToken)
    {
        var retryAfterSeconds = endsAt.HasValue
            ? Math.Max(1, (int)Math.Ceiling((endsAt.Value - _dateTimeProvider.UtcNow).TotalSeconds))
            : (int?)null;

        await _realtimeService.PublishAccountRestrictionAppliedAsync(
            new AccountRestrictionAppliedEvent(
                userId,
                banType.ToString(),
                reason,
                message,
                startedAt,
                endsAt,
                retryAfterSeconds,
                _dateTimeProvider.UtcNow),
            cancellationToken);
    }

    private static void ValidateConfiguration(
        int negativeFeedbackThreshold,
        int negativeRatingMaxScore,
        int temporaryBanDurationDays,
        int maximumTemporaryBans)
    {
        if (negativeFeedbackThreshold <= 0)
        {
            throw new AccountBanException(
                "account_ban.negative_feedback_threshold_invalid",
                "Ngưỡng phản hồi tiêu cực phải lớn hơn 0.",
                StatusCodes.Status400BadRequest);
        }

        if (negativeRatingMaxScore is < 1 or > 5)
        {
            throw new AccountBanException(
                "account_ban.negative_rating_score_invalid",
                "Điểm đánh giá tiêu cực phải nằm trong khoảng từ 1 đến 5.",
                StatusCodes.Status400BadRequest);
        }

        if (temporaryBanDurationDays <= 0)
        {
            throw new AccountBanException(
                "account_ban.temporary_duration_invalid",
                "Thời gian khóa tạm thời phải lớn hơn 0 ngày.",
                StatusCodes.Status400BadRequest);
        }

        if (maximumTemporaryBans <= 0)
        {
            throw new AccountBanException(
                "account_ban.maximum_temporary_bans_invalid",
                "Số lần khóa tạm thời tối đa phải lớn hơn 0.",
                StatusCodes.Status400BadRequest);
        }
    }

    private static AccountBanConfigurationResponse ToResponse(
        AccountBanConfiguration configuration)
    {
        return new AccountBanConfigurationResponse(
            configuration.Id,
            configuration.NegativeFeedbackThreshold,
            configuration.NegativeRatingMaxScore,
            configuration.TemporaryBanDurationDays,
            configuration.MaximumTemporaryBans,
            configuration.IsEnabled,
            configuration.CreatedAt,
            configuration.UpdatedAt,
            configuration.UpdatedByUserId);
    }

    private static string BuildAutomaticBanReason(
        AccountBanType banType,
        AccountBanConfiguration configuration,
        int negativeFeedbackCount)
    {
        return banType == AccountBanType.Permanent
            ? $"Tài khoản bị khóa vĩnh viễn do tiếp tục nhận phản hồi tiêu cực sau {configuration.MaximumTemporaryBans} lần khóa tạm thời."
            : $"Tài khoản bị khóa tạm thời do nhận {negativeFeedbackCount} phản hồi tiêu cực.";
    }

    private static string BuildUserBanMessage(
        AccountBanType banType,
        string reason,
        DateTime utcNow,
        DateTime? endsAt)
    {
        if (banType == AccountBanType.Permanent)
        {
            return $"Tài khoản của bạn đã bị khóa vĩnh viễn. Lý do: {reason}";
        }

        if (banType == AccountBanType.Temporary && endsAt.HasValue)
        {
            var remaining = endsAt.Value - utcNow;
            var days = Math.Max(1, (int)Math.Ceiling(remaining.TotalDays));
            return $"Tài khoản của bạn đang bị khóa tạm thời. Thời gian còn lại khoảng {days} ngày. Lý do: {reason}";
        }

        return $"Tài khoản của bạn đã bị khóa. Lý do: {reason}";
    }

    private static string GetAuthCode(AccountBanHistory restriction)
    {
        return restriction.BanType switch
        {
            AccountBanType.Permanent => "auth.account_permanently_banned",
            AccountBanType.Temporary => "auth.account_temporarily_banned",
            _ => "auth.account_inactive"
        };
    }

    private static int GetRestrictionPriority(AccountBanHistory restriction)
    {
        return restriction.BanType switch
        {
            AccountBanType.Permanent => 3,
            AccountBanType.Temporary => 2,
            AccountBanType.ManualLock => 1,
            _ => 0
        };
    }

    private static int? GetRetryAfterSeconds(
        AccountBanHistory restriction,
        DateTime utcNow)
    {
        if (restriction.BanType != AccountBanType.Temporary ||
            !restriction.EndsAt.HasValue)
        {
            return null;
        }

        var seconds = (restriction.EndsAt.Value - utcNow).TotalSeconds;
        return seconds <= 0
            ? null
            : Math.Min(int.MaxValue, (int)Math.Ceiling(seconds));
    }
}

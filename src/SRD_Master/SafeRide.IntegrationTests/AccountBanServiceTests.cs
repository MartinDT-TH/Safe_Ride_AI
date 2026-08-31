using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Services.AccountBans;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.InMemoryProvider)]
public sealed class AccountBanServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EvaluateRatingAsync_BelowThreshold_DoesNotBanUser()
    {
        await using var fixture = await AccountBanFixture.CreateAsync(
            negativeFeedbackThreshold: 2,
            maximumTemporaryBans: 3);
        var (driverId, _, ratingId) = await fixture.SeedDriverWithRatingsAsync(1);

        await fixture.Service.EvaluateRatingAsync(ratingId, CancellationToken.None);

        var user = await fixture.DbContext.Users.SingleAsync(x => x.Id == driverId);
        Assert.True(user.IsActive);
        Assert.Empty(await fixture.DbContext.AccountBanHistories.ToListAsync());
        Assert.Empty(fixture.SessionRevocation.RevokedUserIds);
        Assert.Empty(fixture.Realtime.Events);
    }

    [Fact]
    public async Task EvaluateRatingAsync_ThresholdReached_CreatesTemporaryBanAndRevokesSessions()
    {
        await using var fixture = await AccountBanFixture.CreateAsync(
            negativeFeedbackThreshold: 2,
            temporaryBanDurationDays: 15,
            maximumTemporaryBans: 3);
        var (driverId, _, ratingId) = await fixture.SeedDriverWithRatingsAsync(2);

        await fixture.Service.EvaluateRatingAsync(ratingId, CancellationToken.None);

        var user = await fixture.DbContext.Users.SingleAsync(x => x.Id == driverId);
        var profile = await fixture.DbContext.DriverProfiles.SingleAsync(x => x.DriverId == driverId);
        var ban = await fixture.DbContext.AccountBanHistories.SingleAsync();

        Assert.False(user.IsActive);
        Assert.Equal(DriverWorkStatus.Offline, profile.WorkStatus);
        Assert.Equal(AccountBanType.Temporary, ban.BanType);
        Assert.Equal(AccountBanSource.AutomaticNegativeFeedback, ban.Source);
        Assert.Equal(AccountBanStatus.Active, ban.Status);
        Assert.Equal(UtcNow.AddDays(15), ban.EndsAt);
        Assert.Equal(2, ban.NegativeFeedbackCount);
        Assert.Equal(1, ban.TemporaryBanSequence);
        Assert.Contains(driverId, fixture.SessionRevocation.RevokedUserIds);
        Assert.Single(fixture.Realtime.Events);
    }

    [Fact]
    public async Task CheckAccountAccessAsync_ExpiredTemporaryBan_ReleasesHistoryAndReactivatesUser()
    {
        await using var fixture = await AccountBanFixture.CreateAsync(
            negativeFeedbackThreshold: 2,
            maximumTemporaryBans: 3);
        var (driverId, _, _) = await fixture.SeedDriverWithRatingsAsync(0);
        var reason = "Khóa tạm thời do kiểm thử";
        var user = await fixture.DbContext.Users.SingleAsync(x => x.Id == driverId);
        user.IsActive = false;
        user.BanReason = reason;
        fixture.DbContext.AccountBanHistories.Add(new AccountBanHistory
        {
            UserId = driverId,
            BanType = AccountBanType.Temporary,
            Source = AccountBanSource.AutomaticNegativeFeedback,
            Status = AccountBanStatus.Active,
            Reason = reason,
            StartedAt = UtcNow.AddDays(-16),
            EndsAt = UtcNow.AddDays(-1),
            CreatedAt = UtcNow.AddDays(-16),
            TemporaryBanSequence = 1
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.CheckAccountAccessAsync(
            driverId,
            releaseExpiredTemporaryBans: true,
            CancellationToken.None);

        var releasedBan = await fixture.DbContext.AccountBanHistories.SingleAsync();
        var reloadedUser = await fixture.DbContext.Users.SingleAsync(x => x.Id == driverId);

        Assert.True(result.IsAllowed);
        Assert.True(reloadedUser.IsActive);
        Assert.Null(reloadedUser.BanReason);
        Assert.Equal(AccountBanStatus.Expired, releasedBan.Status);
    }

    [Fact]
    public async Task EvaluateRatingAsync_MaxTemporaryBansReached_CreatesPermanentBan()
    {
        await using var fixture = await AccountBanFixture.CreateAsync(
            negativeFeedbackThreshold: 1,
            maximumTemporaryBans: 2);
        var (driverId, _, ratingId) = await fixture.SeedDriverWithRatingsAsync(1);
        fixture.DbContext.AccountBanHistories.AddRange(
            CreateExpiredTemporaryBan(driverId, 1, UtcNow.AddDays(-30)),
            CreateExpiredTemporaryBan(driverId, 2, UtcNow.AddDays(-15)));
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.EvaluateRatingAsync(ratingId, CancellationToken.None);

        var permanentBan = await fixture.DbContext.AccountBanHistories
            .Where(x => x.BanType == AccountBanType.Permanent)
            .SingleAsync();

        Assert.Equal(AccountBanStatus.Active, permanentBan.Status);
        Assert.Null(permanentBan.EndsAt);
        Assert.Contains(driverId, fixture.SessionRevocation.RevokedUserIds);
        Assert.Equal(AccountBanType.Permanent.ToString(), fixture.Realtime.Events.Single().BanType);
    }

    private static AccountBanHistory CreateExpiredTemporaryBan(
        Guid driverId,
        int sequence,
        DateTime createdAt)
    {
        return new AccountBanHistory
        {
            UserId = driverId,
            BanType = AccountBanType.Temporary,
            Source = AccountBanSource.AutomaticNegativeFeedback,
            Status = AccountBanStatus.Expired,
            Reason = $"Khóa tạm thời lần {sequence}",
            StartedAt = createdAt,
            EndsAt = createdAt.AddDays(1),
            CreatedAt = createdAt,
            TemporaryBanSequence = sequence
        };
    }

    private sealed class AccountBanFixture : IAsyncDisposable
    {
        private AccountBanFixture(
            ApplicationDbContext dbContext,
            MutableDateTimeProvider clock,
            SessionRevocationFake sessionRevocation,
            AccountRestrictionRealtimeFake realtime)
        {
            DbContext = dbContext;
            Clock = clock;
            SessionRevocation = sessionRevocation;
            Realtime = realtime;
            Service = new AccountBanService(
                dbContext,
                clock,
                sessionRevocation,
                realtime,
                new FakeRedisService(),
                NullLogger<AccountBanService>.Instance);
        }

        public ApplicationDbContext DbContext { get; }
        public MutableDateTimeProvider Clock { get; }
        public SessionRevocationFake SessionRevocation { get; }
        public AccountRestrictionRealtimeFake Realtime { get; }
        public AccountBanService Service { get; }

        public static async Task<AccountBanFixture> CreateAsync(
            int negativeFeedbackThreshold,
            int maximumTemporaryBans,
            int temporaryBanDurationDays = 15,
            int negativeRatingMaxScore = 2)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"account-bans-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var dbContext = new ApplicationDbContext(options, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
            await dbContext.Database.EnsureCreatedAsync();

            var configuration = await dbContext.AccountBanConfigurations
                .SingleAsync(x => x.Id == AccountBanConfiguration.SingletonId);
            configuration.NegativeFeedbackThreshold = negativeFeedbackThreshold;
            configuration.NegativeRatingMaxScore = negativeRatingMaxScore;
            configuration.TemporaryBanDurationDays = temporaryBanDurationDays;
            configuration.MaximumTemporaryBans = maximumTemporaryBans;
            configuration.IsEnabled = true;
            configuration.UpdatedAt = UtcNow;
            await dbContext.SaveChangesAsync();

            return new AccountBanFixture(
                dbContext,
                new MutableDateTimeProvider(UtcNow),
                new SessionRevocationFake(),
                new AccountRestrictionRealtimeFake());
        }

        public async Task<(Guid DriverId, Guid CustomerId, long LastRatingId)> SeedDriverWithRatingsAsync(
            int negativeRatingCount)
        {
            var driverId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            DbContext.Users.AddRange(
                new AspNetUser
                {
                    Id = driverId,
                    UserName = $"driver-{driverId:N}",
                    IsActive = true,
                    CreatedAt = UtcNow
                },
                new AspNetUser
                {
                    Id = customerId,
                    UserName = $"customer-{customerId:N}",
                    IsActive = true,
                    CreatedAt = UtcNow
                });
            DbContext.DriverProfiles.Add(new DriverProfile
            {
                DriverId = driverId,
                IdentityCardNumber = $"cccd-{driverId:N}",
                WorkStatus = DriverWorkStatus.Online,
                CreatedAt = UtcNow
            });

            long lastRatingId = 0;
            for (var index = 1; index <= negativeRatingCount; index++)
            {
                lastRatingId = index;
                DbContext.Ratings.Add(new Rating
                {
                    Id = index,
                    TripId = index,
                    DriverId = driverId,
                    CustomerId = customerId,
                    RatingScore = 1,
                    CreatedAt = UtcNow.AddMinutes(-negativeRatingCount + index)
                });
            }

            await DbContext.SaveChangesAsync();
            return (driverId, customerId, lastRatingId);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
        }
    }

    private sealed class MutableDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class SessionRevocationFake : IUserSessionRevocationService
    {
        public List<Guid> RevokedUserIds { get; } = [];

        public Task RevokeAllUserSessionsAsync(
            Guid userId,
            string reason,
            CancellationToken cancellationToken)
        {
            RevokedUserIds.Add(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class AccountRestrictionRealtimeFake : IAccountRestrictionRealtimeService
    {
        public List<AccountRestrictionAppliedEvent> Events { get; } = [];

        public Task PublishAccountRestrictionAppliedAsync(
            AccountRestrictionAppliedEvent notification,
            CancellationToken cancellationToken = default)
        {
            Events.Add(notification);
            return Task.CompletedTask;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Features.Promotions;
using SafeRide.Application.Features.Promotions.Commands.CreateAdminPromotion;
using SafeRide.Application.Features.Promotions.Commands.UpdateAdminPromotion;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Repositories;

namespace SafeRide.IntegrationTests;

public sealed class AdminPromotionTests
{
    private static readonly DateTime StartDate =
        new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreatePromotion_ValidCommand_PersistsPromotion()
    {
        await using var fixture = CreateFixture();

        var response = await fixture.CreateHandler.Handle(
            CreateCommand(" safe20 "),
            CancellationToken.None);

        var promotion = await fixture.DbContext.Promotions.SingleAsync();
        Assert.Equal("SAFE20", response.PromotionCode);
        Assert.Equal("SAFE20", promotion.PromotionCode);
        Assert.Equal(0, promotion.CurrentUsageCount);
        Assert.True(response.Id > 0);
        Assert.Equal(3, response.RequiredCompletedTrips);
        Assert.Equal(3, fixture.UnlockRuleStore.Get("SAFE20"));
    }

    [Fact]
    public async Task CreatePromotion_DuplicateCode_ThrowsConflict()
    {
        await using var fixture = CreateFixture();
        fixture.DbContext.Promotions.Add(CreatePromotion("SAFE20"));
        await fixture.DbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PromotionException>(
            () => fixture.CreateHandler.Handle(
                CreateCommand("safe20"),
                CancellationToken.None));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("admin_promotion.code_conflict", exception.Code);
    }

    [Fact]
    public async Task UpdatePromotion_ValidCommand_UpdatesWithoutResettingUsage()
    {
        await using var fixture = CreateFixture();
        var promotion = CreatePromotion("OLD20");
        promotion.CurrentUsageCount = 7;
        fixture.DbContext.Promotions.Add(promotion);
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.UpdateHandler.Handle(
            UpdateCommand(promotion.Id, "new20"),
            CancellationToken.None);

        Assert.Equal("NEW20", response.PromotionCode);
        Assert.Equal(7, response.CurrentUsageCount);
        Assert.Equal(7, promotion.CurrentUsageCount);
        Assert.Equal(25m, promotion.DiscountValue);
        Assert.Equal(3, fixture.UnlockRuleStore.Get("NEW20"));
        Assert.Equal(0, fixture.UnlockRuleStore.Get("OLD20"));
    }

    [Fact]
    public async Task UpdatePromotion_RequiredTripsZero_RemovesRule()
    {
        await using var fixture = CreateFixture();
        var promotion = CreatePromotion("SAFE20");
        fixture.DbContext.Promotions.Add(promotion);
        await fixture.DbContext.SaveChangesAsync();
        await fixture.UnlockRuleStore.SaveAsync("SAFE20", 4, CancellationToken.None);

        var response = await fixture.UpdateHandler.Handle(
            UpdateCommand(promotion.Id, "SAFE20") with { RequiredCompletedTrips = 0 },
            CancellationToken.None);

        Assert.Equal(0, response.RequiredCompletedTrips);
        Assert.Equal(0, fixture.UnlockRuleStore.Get("SAFE20"));
    }

    [Fact]
    public async Task CreatePromotion_NegativeRequiredTrips_ThrowsBadRequest()
    {
        await using var fixture = CreateFixture();

        var exception = await Assert.ThrowsAsync<PromotionException>(() =>
            fixture.CreateHandler.Handle(
                CreateCommand("SAFE20") with { RequiredCompletedTrips = -1 },
                CancellationToken.None));

        Assert.Equal("admin_promotion.invalid_required_completed_trips", exception.Code);
    }

    [Fact]
    public async Task UpdatePromotion_MissingPromotion_ThrowsNotFound()
    {
        await using var fixture = CreateFixture();

        var exception = await Assert.ThrowsAsync<PromotionException>(
            () => fixture.UpdateHandler.Handle(
                UpdateCommand(999, "SAFE20"),
                CancellationToken.None));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("admin_promotion.not_found", exception.Code);
    }

    [Fact]
    public async Task CreatePromotion_PercentageOverOneHundred_ThrowsBadRequest()
    {
        await using var fixture = CreateFixture();
        var command = CreateCommand("SAFE101") with
        {
            DiscountValue = 101m
        };

        var exception = await Assert.ThrowsAsync<PromotionException>(
            () => fixture.CreateHandler.Handle(command, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("admin_promotion.percentage_exceeds_limit", exception.Code);
    }

    [Theory]
    [InlineData(0, 1, "admin_promotion.invalid_max_usage_count")]
    [InlineData(100, 0, "admin_promotion.invalid_user_usage_limit")]
    public async Task CreatePromotion_NonPositiveUsageLimit_ThrowsBadRequest(
        int maxUsageCount,
        int usageLimitPerUser,
        string expectedCode)
    {
        await using var fixture = CreateFixture();
        var command = CreateCommand("LIMIT") with
        {
            MaxUsageCount = maxUsageCount,
            UsageLimitPerUser = usageLimitPerUser
        };

        var exception = await Assert.ThrowsAsync<PromotionException>(
            () => fixture.CreateHandler.Handle(command, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public async Task CreatePromotion_EqualStartAndEndDate_ThrowsBadRequest()
    {
        await using var fixture = CreateFixture();
        var command = CreateCommand("SAMEDATE") with
        {
            EndDate = StartDate
        };

        var exception = await Assert.ThrowsAsync<PromotionException>(
            () => fixture.CreateHandler.Handle(command, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("admin_promotion.invalid_date_range", exception.Code);
    }

    [Fact]
    public async Task UpdatePromotion_MaxUsageBelowCurrentUsage_ThrowsBadRequest()
    {
        await using var fixture = CreateFixture();
        var promotion = CreatePromotion("SAFE20");
        promotion.CurrentUsageCount = 8;
        fixture.DbContext.Promotions.Add(promotion);
        await fixture.DbContext.SaveChangesAsync();
        var command = UpdateCommand(promotion.Id, "SAFE20") with
        {
            MaxUsageCount = 7
        };

        var exception = await Assert.ThrowsAsync<PromotionException>(
            () => fixture.UpdateHandler.Handle(command, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(
            "admin_promotion.max_usage_below_current_usage",
            exception.Code);
    }

    private static AdminPromotionFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"admin-promotions-{Guid.NewGuid():N}")
            .Options;
        var dbContext = new ApplicationDbContext(options);
        var repository = new PromotionRepository(dbContext);
        var unitOfWork = new UnitOfWork(dbContext);
        var unlockRuleStore = new PromotionUnlockRuleStoreFake();

        return new AdminPromotionFixture(
            dbContext,
            unlockRuleStore,
            new CreateAdminPromotionCommandHandler(repository, unitOfWork, unlockRuleStore),
            new UpdateAdminPromotionCommandHandler(repository, unitOfWork, unlockRuleStore));
    }

    private static CreateAdminPromotionCommand CreateCommand(string code)
    {
        return new CreateAdminPromotionCommand(
            code,
            DiscountType.Percentage,
            20m,
            StartDate,
            StartDate.AddDays(30),
            100,
            100_000m,
            50_000m,
            1,
            3,
            true);
    }

    private static UpdateAdminPromotionCommand UpdateCommand(
        long promotionId,
        string code)
    {
        return new UpdateAdminPromotionCommand(
            promotionId,
            code,
            DiscountType.Percentage,
            25m,
            StartDate,
            StartDate.AddDays(45),
            200,
            120_000m,
            60_000m,
            2,
            3,
            true);
    }

    private static Promotion CreatePromotion(string code)
    {
        return new Promotion
        {
            PromotionCode = code,
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20m,
            StartDate = StartDate,
            EndDate = StartDate.AddDays(30),
            MaxUsageCount = 100,
            CurrentUsageCount = 0,
            MinimumOrderValue = 100_000m,
            MaximumDiscountValue = 50_000m,
            UsageLimitPerUser = 1,
            IsActive = true
        };
    }

    private sealed record AdminPromotionFixture(
        ApplicationDbContext DbContext,
        PromotionUnlockRuleStoreFake UnlockRuleStore,
        CreateAdminPromotionCommandHandler CreateHandler,
        UpdateAdminPromotionCommandHandler UpdateHandler) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }

    private sealed class PromotionUnlockRuleStoreFake : SafeRide.Application.Common.Interfaces.IPromotionUnlockRuleStore
    {
        private readonly Dictionary<string, int> _rules = new(StringComparer.Ordinal);

        public int Get(string promotionCode) =>
            _rules.GetValueOrDefault(promotionCode.Trim().ToUpperInvariant());

        public Task<int> GetRequiredCompletedTripsAsync(
            string promotionCode,
            CancellationToken cancellationToken) => Task.FromResult(Get(promotionCode));

        public Task<IReadOnlyDictionary<string, int>> GetRequiredCompletedTripsAsync(
            IReadOnlyCollection<string> promotionCodes,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, int>>(
                promotionCodes.ToDictionary(code => code, Get));

        public Task SaveAsync(
            string promotionCode,
            int requiredCompletedTrips,
            CancellationToken cancellationToken)
        {
            _rules[promotionCode.Trim().ToUpperInvariant()] = requiredCompletedTrips;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            string promotionCode,
            CancellationToken cancellationToken)
        {
            _rules.Remove(promotionCode.Trim().ToUpperInvariant());
            return Task.CompletedTask;
        }
    }
}

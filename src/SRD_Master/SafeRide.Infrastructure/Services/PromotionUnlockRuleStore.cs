using System.Globalization;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Services;

public sealed class PromotionUnlockRuleStore : IPromotionUnlockRuleStore
{
    private const string KeyPrefix = "sr:promotion:unlock-required-trips:";
    private readonly IRedisService _redisService;

    public PromotionUnlockRuleStore(IRedisService redisService)
    {
        _redisService = redisService;
    }

    public async Task<int> GetRequiredCompletedTripsAsync(
        string promotionCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _redisService.GetAsync(CreateKey(promotionCode));
        return Parse(value);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetRequiredCompletedTripsAsync(
        IReadOnlyCollection<string> promotionCodes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedCodes = promotionCodes
            .Select(NormalizeCode)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var keys = normalizedCodes.Select(CreateKey).ToList();
        var values = await _redisService.GetManyAsync(keys);

        return normalizedCodes.ToDictionary(
            code => code,
            code => Parse(values.GetValueOrDefault(CreateKey(code))),
            StringComparer.Ordinal);
    }

    public async Task SaveAsync(
        string promotionCode,
        int requiredCompletedTrips,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redisService.SetPersistentAsync(
            CreateKey(promotionCode),
            requiredCompletedTrips.ToString(CultureInfo.InvariantCulture));
    }

    public async Task RemoveAsync(
        string promotionCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _redisService.RemoveAsync(CreateKey(promotionCode));
    }

    private static string CreateKey(string promotionCode) =>
        $"{KeyPrefix}{NormalizeCode(promotionCode)}";

    private static string NormalizeCode(string promotionCode) =>
        promotionCode.Trim().ToUpperInvariant();

    private static int Parse(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            && result > 0
                ? result
                : 0;
}

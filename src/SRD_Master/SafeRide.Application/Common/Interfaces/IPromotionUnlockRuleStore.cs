namespace SafeRide.Application.Common.Interfaces;

public interface IPromotionUnlockRuleStore
{
    Task<int> GetRequiredCompletedTripsAsync(
        string promotionCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, int>> GetRequiredCompletedTripsAsync(
        IReadOnlyCollection<string> promotionCodes,
        CancellationToken cancellationToken);

    Task SaveAsync(
        string promotionCode,
        int requiredCompletedTrips,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        string promotionCode,
        CancellationToken cancellationToken);
}

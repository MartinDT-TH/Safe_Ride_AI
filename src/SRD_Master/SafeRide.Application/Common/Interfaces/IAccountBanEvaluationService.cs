namespace SafeRide.Application.Common.Interfaces;

public interface IAccountBanEvaluationService
{
    Task EvaluateRatingAsync(long ratingId, CancellationToken cancellationToken);
}

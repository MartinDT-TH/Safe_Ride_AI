namespace SafeRide.Contracts.Responses.Feedbacks;

public sealed record DriverRatingSummaryResponse(
    Guid DriverId,
    double AverageRating,
    int TotalRatings,
    IReadOnlyList<DriverRatingItemResponse> Ratings);

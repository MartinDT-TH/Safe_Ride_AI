namespace SafeRide.Contracts.Responses.Feedbacks;

public sealed record DriverRatingItemResponse(
    long Id,
    long TripId,
    string? CustomerName,
    string? CustomerAvatarUrl,
    int Score,
    string? Comment,
    DateTime CreatedAt);

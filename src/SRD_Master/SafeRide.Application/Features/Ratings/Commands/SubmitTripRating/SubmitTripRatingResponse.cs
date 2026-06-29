namespace SafeRide.Application.Features.Ratings.Commands.SubmitTripRating;

public sealed record SubmitTripRatingResponse(
    long TripId,
    int RatingScore,
    string? Comment,
    DateTime CreatedAt,
    string Message);

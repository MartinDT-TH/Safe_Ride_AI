namespace SafeRide.Contracts.Requests.Trips;

public sealed record SubmitTripRatingRequest(
    int RatingScore,
    string? Comment);

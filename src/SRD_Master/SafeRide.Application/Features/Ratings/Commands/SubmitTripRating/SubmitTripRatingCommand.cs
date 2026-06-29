using MediatR;

namespace SafeRide.Application.Features.Ratings.Commands.SubmitTripRating;

public sealed record SubmitTripRatingCommand(
    long TripId,
    Guid CustomerId,
    int RatingScore,
    string? Comment)
    : IRequest<SubmitTripRatingResponse>;

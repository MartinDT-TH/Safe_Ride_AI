using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Ratings.Commands.SubmitTripRating;

public sealed class SubmitTripRatingCommandHandler
    : IRequestHandler<SubmitTripRatingCommand, SubmitTripRatingResponse>
{
    private readonly IRatingRepository _ratingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SubmitTripRatingCommandHandler(
        IRatingRepository ratingRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _ratingRepository = ratingRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<SubmitTripRatingResponse> Handle(
        SubmitTripRatingCommand request,
        CancellationToken cancellationToken)
    {
        ValidateRatingScore(request.RatingScore);

        var trip = await _ratingRepository.GetTripForRatingAsync(
            request.TripId,
            cancellationToken);
        if (trip is null)
        {
            throw new RatingException(
                "rating.trip_not_found",
                "Không tìm thấy chuyến đi.",
                404);
        }

        ValidateCustomerCanRate(trip, request.CustomerId);
        ValidateTripCompleted(trip);
        ValidateNotRated(trip);

        var comment = NormalizeComment(request.Comment);
        var utcNow = _dateTimeProvider.UtcNow;
        var rating = new Rating
        {
            TripId = trip.Id,
            CustomerId = request.CustomerId,
            DriverId = trip.DriverId,
            RatingScore = request.RatingScore,
            Comment = comment,
            CreatedAt = utcNow
        };

        await _ratingRepository.AddRatingAsync(rating, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubmitTripRatingResponse(
            trip.Id,
            request.RatingScore,
            comment,
            utcNow,
            "Cảm ơn bạn đã đánh giá tài xế.");
    }

    private static void ValidateRatingScore(int ratingScore)
    {
        if (ratingScore is < 1 or > 5)
        {
            throw new RatingException(
                "rating.invalid_score",
                "Điểm đánh giá phải từ 1 đến 5.",
                400);
        }
    }

    private static void ValidateCustomerCanRate(Trip trip, Guid customerId)
    {
        if (trip.Booking.CustomerId != customerId)
        {
            throw new RatingException(
                "rating.forbidden",
                "Bạn không có quyền đánh giá chuyến đi này.",
                403);
        }
    }

    private static void ValidateTripCompleted(Trip trip)
    {
        if (trip.TripStatus != TripStatus.COMPLETED)
        {
            throw new RatingException(
                "rating.trip_not_completed",
                "Chỉ có thể đánh giá sau khi chuyến đi hoàn thành.",
                409);
        }
    }

    private static void ValidateNotRated(Trip trip)
    {
        if (trip.Rating is not null)
        {
            throw new RatingException(
                "rating.already_submitted",
                "Chuyến đi này đã được đánh giá.",
                409);
        }
    }

    private static string? NormalizeComment(string? comment)
    {
        var normalizedComment = comment?.Trim();
        return string.IsNullOrWhiteSpace(normalizedComment)
            ? null
            : normalizedComment;
    }
}

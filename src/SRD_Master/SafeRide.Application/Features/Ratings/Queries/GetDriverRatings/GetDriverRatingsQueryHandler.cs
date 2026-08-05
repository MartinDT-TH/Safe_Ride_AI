using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Feedbacks;

namespace SafeRide.Application.Features.Ratings.Queries.GetDriverRatings;

public sealed class GetDriverRatingsQueryHandler
    : IRequestHandler<GetDriverRatingsQuery, DriverRatingSummaryResponse>
{
    private readonly IRatingRepository _ratingRepository;

    public GetDriverRatingsQueryHandler(IRatingRepository ratingRepository)
    {
        _ratingRepository = ratingRepository;
    }

    public async Task<DriverRatingSummaryResponse> Handle(
        GetDriverRatingsQuery request,
        CancellationToken cancellationToken)
    {
        var driverExists = await _ratingRepository.DriverExistsAsync(
            request.DriverId,
            cancellationToken);

        if (!driverExists)
        {
            throw new RatingException(
                "rating.driver_not_found",
                "Không tìm thấy tài xế.",
                404);
        }

        var items = await _ratingRepository.GetDriverRatingItemsAsync(
            request.DriverId,
            cancellationToken);

        var averageRating = items.Count > 0
            ? Math.Round(items.Average(r => r.Score), 2)
            : 0.0;

        return new DriverRatingSummaryResponse(
            request.DriverId,
            averageRating,
            items.Count,
            items);
    }
}

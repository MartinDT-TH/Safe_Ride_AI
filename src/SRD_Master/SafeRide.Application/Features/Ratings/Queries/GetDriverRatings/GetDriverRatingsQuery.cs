using MediatR;
using SafeRide.Contracts.Responses.Feedbacks;

namespace SafeRide.Application.Features.Ratings.Queries.GetDriverRatings;

public sealed record GetDriverRatingsQuery(Guid DriverId)
    : IRequest<DriverRatingSummaryResponse>;

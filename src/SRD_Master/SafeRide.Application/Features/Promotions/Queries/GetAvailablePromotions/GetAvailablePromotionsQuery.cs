using MediatR;
using SafeRide.Contracts.Responses.Promotions;

namespace SafeRide.Application.Features.Promotions.Queries.GetAvailablePromotions;

public sealed record GetAvailablePromotionsQuery(Guid CustomerId)
    : IRequest<IReadOnlyList<AvailablePromotionResponse>>;

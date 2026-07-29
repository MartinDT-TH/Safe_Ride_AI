using MediatR;
using SafeRide.Contracts.Responses.Promotions;

namespace SafeRide.Application.Features.Promotions.Queries.GetAdminPromotions;

public sealed record GetAdminPromotionsQuery(
    int Page,
    int PageSize,
    string? Search,
    string? Status) : IRequest<AdminPromotionsPageResponse>;

using MediatR;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingCatalog;

public sealed record GetBookingCatalogQuery(Guid CustomerId)
    : IRequest<GetBookingCatalogResult>;

using MediatR;

namespace SafeRide.Application.Features.AdminBookings.Queries.GetAdminBookings;

public sealed record GetAdminBookingsQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    string? Status = null,
    string? SortBy = null,
    string? SortDirection = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IRequest<AdminBookingPagedResult>;

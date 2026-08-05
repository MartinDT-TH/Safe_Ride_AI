using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminBookings.Queries.GetAdminBookings;

public sealed class GetAdminBookingsQueryHandler
    : IRequestHandler<GetAdminBookingsQuery, AdminBookingPagedResult>
{
    private readonly IAdminBookingManagementService _adminBookingManagementService;

    public GetAdminBookingsQueryHandler(
        IAdminBookingManagementService adminBookingManagementService)
    {
        _adminBookingManagementService = adminBookingManagementService;
    }

    public Task<AdminBookingPagedResult> Handle(
        GetAdminBookingsQuery request,
        CancellationToken cancellationToken)
    {
        return _adminBookingManagementService.GetBookingsAsync(
            new AdminBookingListFilter(
                request.Page,
                request.PageSize,
                request.Search,
                request.Status,
                request.SortBy,
                request.SortDirection,
                request.FromDate,
                request.ToDate),
            cancellationToken);
    }
}

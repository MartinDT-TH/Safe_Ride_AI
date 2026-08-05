using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminTrips.Queries.GetAdminTripDetailsByBooking;

public sealed class GetAdminTripDetailsByBookingQueryHandler
    : IRequestHandler<GetAdminTripDetailsByBookingQuery, AdminTripDetailsResponse?>
{
    private readonly IAdminTripManagementService _adminTripManagementService;

    public GetAdminTripDetailsByBookingQueryHandler(
        IAdminTripManagementService adminTripManagementService)
    {
        _adminTripManagementService = adminTripManagementService;
    }

    public Task<AdminTripDetailsResponse?> Handle(
        GetAdminTripDetailsByBookingQuery request,
        CancellationToken cancellationToken)
    {
        return _adminTripManagementService.GetTripDetailsByBookingIdAsync(
            request.BookingId,
            cancellationToken);
    }
}

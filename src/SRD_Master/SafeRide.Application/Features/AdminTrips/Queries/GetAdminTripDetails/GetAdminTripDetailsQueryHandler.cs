using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminTrips.Queries.GetAdminTripDetails;

public sealed class GetAdminTripDetailsQueryHandler
    : IRequestHandler<GetAdminTripDetailsQuery, AdminTripDetailsResponse?>
{
    private readonly IAdminTripManagementService _adminTripManagementService;

    public GetAdminTripDetailsQueryHandler(
        IAdminTripManagementService adminTripManagementService)
    {
        _adminTripManagementService = adminTripManagementService;
    }

    public Task<AdminTripDetailsResponse?> Handle(
        GetAdminTripDetailsQuery request,
        CancellationToken cancellationToken)
    {
        return _adminTripManagementService.GetTripDetailsByTripIdAsync(
            request.TripId,
            cancellationToken);
    }
}

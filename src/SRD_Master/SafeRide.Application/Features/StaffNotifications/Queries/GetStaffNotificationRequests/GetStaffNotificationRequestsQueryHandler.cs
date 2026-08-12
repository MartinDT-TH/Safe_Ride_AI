using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.StaffNotifications.Queries.GetStaffNotificationRequests;

public sealed class GetStaffNotificationRequestsQueryHandler
    : IRequestHandler<GetStaffNotificationRequestsQuery, StaffNotificationRequestPagedResult>
{
    private readonly IStaffNotificationRequestService _staffNotificationRequestService;

    public GetStaffNotificationRequestsQueryHandler(
        IStaffNotificationRequestService staffNotificationRequestService)
    {
        _staffNotificationRequestService = staffNotificationRequestService;
    }

    public Task<StaffNotificationRequestPagedResult> Handle(
        GetStaffNotificationRequestsQuery request,
        CancellationToken cancellationToken)
    {
        return _staffNotificationRequestService.GetRequestsAsync(
            new StaffNotificationRequestListFilter(
                request.CreatedBy,
                request.Page,
                request.PageSize,
                request.Search,
                request.Status,
                request.Type,
                request.Audience),
            cancellationToken);
    }
}

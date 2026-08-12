using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.StaffPayments.Queries.GetStaffPaymentStatuses;

public sealed class GetStaffPaymentStatusesQueryHandler
    : IRequestHandler<GetStaffPaymentStatusesQuery, StaffPaymentStatusPagedResult>
{
    private readonly IStaffPaymentStatusService _staffPaymentStatusService;

    public GetStaffPaymentStatusesQueryHandler(
        IStaffPaymentStatusService staffPaymentStatusService)
    {
        _staffPaymentStatusService = staffPaymentStatusService;
    }

    public Task<StaffPaymentStatusPagedResult> Handle(
        GetStaffPaymentStatusesQuery request,
        CancellationToken cancellationToken)
    {
        return _staffPaymentStatusService.GetPaymentStatusesAsync(
            new StaffPaymentStatusListFilter(
                request.Page,
                request.PageSize,
                request.Status,
                request.Method,
                request.Date),
            cancellationToken);
    }
}

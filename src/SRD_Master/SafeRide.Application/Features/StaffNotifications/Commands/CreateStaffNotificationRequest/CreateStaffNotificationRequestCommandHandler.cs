using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.StaffNotifications.Commands.CreateStaffNotificationRequest;

public sealed class CreateStaffNotificationRequestCommandHandler
    : IRequestHandler<CreateStaffNotificationRequestCommand, StaffNotificationRequestResponse>
{
    private readonly IStaffNotificationRequestService _staffNotificationRequestService;

    public CreateStaffNotificationRequestCommandHandler(
        IStaffNotificationRequestService staffNotificationRequestService)
    {
        _staffNotificationRequestService = staffNotificationRequestService;
    }

    public Task<StaffNotificationRequestResponse> Handle(
        CreateStaffNotificationRequestCommand request,
        CancellationToken cancellationToken)
    {
        return _staffNotificationRequestService.CreateRequestAsync(
            request.CreatedBy,
            request.Title,
            request.Content,
            request.NotificationType,
            request.TargetAudience,
            cancellationToken);
    }
}

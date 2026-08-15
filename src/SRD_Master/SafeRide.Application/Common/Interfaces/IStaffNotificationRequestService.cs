using SafeRide.Application.Features.StaffNotifications;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface IStaffNotificationRequestService
{
    Task<StaffNotificationRequestPagedResult> GetRequestsAsync(
        StaffNotificationRequestListFilter filter,
        CancellationToken cancellationToken);

    Task<StaffNotificationRequestResponse> CreateRequestAsync(
        Guid createdBy,
        string title,
        string content,
        string notificationType,
        NotificationAudience targetAudience,
        CancellationToken cancellationToken);
}

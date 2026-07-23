using SafeRide.Application.Features.AdminNotifications;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface IAdminNotificationManagementService
{
    Task<AdminNotificationPagedResult> GetNotificationsAsync(
        AdminNotificationListFilter filter,
        CancellationToken cancellationToken);

    Task<AdminNotificationResponse> CreateNotificationAsync(
        Guid createdBy,
        string title,
        string content,
        string notificationType,
        NotificationAudience targetAudience,
        CancellationToken cancellationToken);

    Task<AdminNotificationResponse> ApproveNotificationAsync(
        long notificationId,
        Guid approvedBy,
        CancellationToken cancellationToken);

    Task<AdminNotificationResponse> RejectNotificationAsync(
        long notificationId,
        Guid rejectedBy,
        string rejectionReason,
        CancellationToken cancellationToken);
}

import '../../data/models/system_notification_item.dart';
import '../../data/models/system_notifications_page.dart';

abstract class NotificationRepository {
  Future<SystemNotificationsPage> getNotifications(
    String accessToken, {
    int page = 1,
    int pageSize = 20,
  });

  Future<SystemNotificationItem> markAsRead(
    String accessToken,
    int notificationId,
  );
}

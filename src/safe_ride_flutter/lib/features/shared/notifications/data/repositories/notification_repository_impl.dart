import '../../domain/repositories/notification_repository.dart';
import '../datasources/notification_remote_datasource.dart';
import '../models/system_notification_item.dart';
import '../models/system_notifications_page.dart';

class NotificationRepositoryImpl implements NotificationRepository {
  const NotificationRepositoryImpl(this._remoteDatasource);

  final NotificationRemoteDatasource _remoteDatasource;

  @override
  Future<SystemNotificationsPage> getNotifications(
    String accessToken, {
    int page = 1,
    int pageSize = 20,
  }) {
    return _remoteDatasource.getNotifications(
      accessToken,
      page: page,
      pageSize: pageSize,
    );
  }

  @override
  Future<SystemNotificationItem> markAsRead(
    String accessToken,
    int notificationId,
  ) {
    return _remoteDatasource.markAsRead(accessToken, notificationId);
  }
}

import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/services/socket_service.dart';
import 'package:safe_ride/features/shared/notifications/data/models/system_notification_item.dart';

void main() {
  test('notification API model keeps trip-share reference id', () {
    final item = SystemNotificationItem.fromJson({
      'id': 12,
      'title': 'Chuyến đi được chia sẻ',
      'content': 'Một chuyến đi đã được chia sẻ với bạn.',
      'notificationType': 'TripShared',
      'referenceId': 42,
      'isRead': false,
      'sentAt': '2026-08-11T10:00:00Z',
    });

    expect(item.notificationType, 'TripShared');
    expect(item.referenceId, 42);
  });

  test('notification SignalR model keeps trip-share reference id', () {
    final update = SystemNotificationUpdate.fromSignalRArguments([
      {
        'id': 12,
        'title': 'Chuyến đi được chia sẻ',
        'content': 'Một chuyến đi đã được chia sẻ với bạn.',
        'notificationType': 'TripShared',
        'referenceId': 42,
        'sentAt': '2026-08-11T10:00:00Z',
      },
    ]);

    expect(update, isNotNull);
    expect(update!.notificationType, 'TripShared');
    expect(update.referenceId, 42);
  });
}

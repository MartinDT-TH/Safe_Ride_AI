import 'system_notification_item.dart';

class SystemNotificationsPage {
  const SystemNotificationsPage({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalItems,
    required this.totalPages,
    required this.unreadCount,
  });

  final List<SystemNotificationItem> items;
  final int page;
  final int pageSize;
  final int totalItems;
  final int totalPages;
  final int unreadCount;

  bool get hasMore => page < totalPages;

  factory SystemNotificationsPage.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'];
    final items = rawItems is List
        ? rawItems
            .map(
              (item) => SystemNotificationItem.fromJson(
                Map<String, dynamic>.from(item as Map),
              ),
            )
            .toList()
        : <SystemNotificationItem>[];

    return SystemNotificationsPage(
      items: items,
      page: (json['page'] as num?)?.toInt() ?? 1,
      pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
      totalItems: (json['totalItems'] as num?)?.toInt() ?? items.length,
      totalPages: (json['totalPages'] as num?)?.toInt() ?? 1,
      unreadCount: (json['unreadCount'] as num?)?.toInt() ?? 0,
    );
  }
}

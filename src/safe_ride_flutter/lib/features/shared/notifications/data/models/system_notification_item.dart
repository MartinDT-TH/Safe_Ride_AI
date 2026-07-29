class SystemNotificationItem {
  const SystemNotificationItem({
    required this.id,
    required this.title,
    required this.content,
    required this.notificationType,
    required this.isRead,
    required this.sentAt,
    this.readAt,
  });

  final int id;
  final String title;
  final String content;
  final String notificationType;
  final bool isRead;
  final DateTime sentAt;
  final DateTime? readAt;

  factory SystemNotificationItem.fromJson(Map<String, dynamic> json) {
    return SystemNotificationItem(
      id: (json['id'] as num?)?.toInt() ?? 0,
      title: json['title']?.toString() ?? '',
      content: json['content']?.toString() ?? '',
      notificationType: json['notificationType']?.toString() ?? 'System Update',
      isRead: json['isRead'] == true,
      sentAt: DateTime.tryParse(json['sentAt']?.toString() ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0),
      readAt: json['readAt'] == null
          ? null
          : DateTime.tryParse(json['readAt'].toString()),
    );
  }

  SystemNotificationItem copyWith({
    int? id,
    String? title,
    String? content,
    String? notificationType,
    bool? isRead,
    DateTime? sentAt,
    DateTime? readAt,
  }) {
    return SystemNotificationItem(
      id: id ?? this.id,
      title: title ?? this.title,
      content: content ?? this.content,
      notificationType: notificationType ?? this.notificationType,
      isRead: isRead ?? this.isRead,
      sentAt: sentAt ?? this.sentAt,
      readAt: readAt ?? this.readAt,
    );
  }
}

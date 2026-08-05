import 'dart:convert';

class SystemNotificationItem {
  const SystemNotificationItem({
    required this.id,
    required this.title,
    required this.content,
    required this.notificationType,
    required this.isRead,
    required this.sentAt,
    this.translations = const {},
    this.readAt,
  });

  final int id;
  final String title;
  final String content;
  final String notificationType;
  final bool isRead;
  final DateTime sentAt;
  final Map<String, NotificationTranslation> translations;
  final DateTime? readAt;

  factory SystemNotificationItem.fromJson(Map<String, dynamic> json) {
    return SystemNotificationItem(
      id: (json['id'] as num?)?.toInt() ?? 0,
      title: json['title']?.toString() ?? '',
      content: json['content']?.toString() ?? '',
      notificationType: json['notificationType']?.toString() ?? 'System Update',
      isRead: json['isRead'] == true,
      sentAt:
          DateTime.tryParse(json['sentAt']?.toString() ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0),
      translations: _readTranslations(json['translationsJson']),
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
    Map<String, NotificationTranslation>? translations,
    DateTime? readAt,
  }) {
    return SystemNotificationItem(
      id: id ?? this.id,
      title: title ?? this.title,
      content: content ?? this.content,
      notificationType: notificationType ?? this.notificationType,
      isRead: isRead ?? this.isRead,
      sentAt: sentAt ?? this.sentAt,
      translations: translations ?? this.translations,
      readAt: readAt ?? this.readAt,
    );
  }

  String localizedTitle(String languageCode) =>
      translations[languageCode]?.title ?? title;

  String localizedContent(String languageCode) =>
      translations[languageCode]?.content ?? content;

  static Map<String, NotificationTranslation> _readTranslations(dynamic value) {
    if (value == null) return const {};
    try {
      final decoded = value is String ? jsonDecode(value) : value;
      if (decoded is! Map) return const {};
      return decoded.map((key, translation) {
        final data = translation as Map;
        return MapEntry(
          key.toString(),
          NotificationTranslation(
            title: data['Title']?.toString() ?? data['title']?.toString() ?? '',
            content:
                data['Content']?.toString() ??
                data['content']?.toString() ??
                '',
          ),
        );
      });
    } catch (_) {
      return const {};
    }
  }
}

class NotificationTranslation {
  const NotificationTranslation({required this.title, required this.content});

  final String title;
  final String content;
}

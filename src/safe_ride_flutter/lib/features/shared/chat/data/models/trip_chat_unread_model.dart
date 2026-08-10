class TripChatUnreadSummaryModel {
  const TripChatUnreadSummaryModel({
    required this.totalUnread,
    required this.items,
  });

  final int totalUnread;
  final List<TripChatUnreadItemModel> items;

  factory TripChatUnreadSummaryModel.fromJson(Map<String, dynamic> json) {
    final rawItems = json['items'];
    return TripChatUnreadSummaryModel(
      totalUnread: (json['totalUnread'] as num?)?.toInt() ?? 0,
      items: rawItems is List
          ? rawItems
                .whereType<Map>()
                .map(
                  (item) => TripChatUnreadItemModel.fromJson(
                    Map<String, dynamic>.from(item),
                  ),
                )
                .toList()
          : const [],
    );
  }
}

class TripChatUnreadItemModel {
  const TripChatUnreadItemModel({
    required this.tripId,
    required this.bookingId,
    required this.unreadCount,
    required this.lastMessagePreview,
    required this.lastMessageAt,
  });

  final int tripId;
  final int bookingId;
  final int unreadCount;
  final String lastMessagePreview;
  final DateTime lastMessageAt;

  factory TripChatUnreadItemModel.fromJson(Map<String, dynamic> json) {
    return TripChatUnreadItemModel(
      tripId: (json['tripId'] as num?)?.toInt() ?? 0,
      bookingId: (json['bookingId'] as num?)?.toInt() ?? 0,
      unreadCount: (json['unreadCount'] as num?)?.toInt() ?? 0,
      lastMessagePreview: json['lastMessagePreview']?.toString() ?? '',
      lastMessageAt:
          DateTime.tryParse(json['lastMessageAt']?.toString() ?? '') ??
          DateTime.now(),
    );
  }
}

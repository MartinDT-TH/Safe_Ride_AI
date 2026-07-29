class DriverRatingItem {
  final int id;
  final int tripId;
  final String customerName;
  final String? customerAvatarUrl;
  final int score;
  final String? comment;
  final DateTime createdAt;

  const DriverRatingItem({
    required this.id,
    required this.tripId,
    required this.customerName,
    this.customerAvatarUrl,
    required this.score,
    this.comment,
    required this.createdAt,
  });

  factory DriverRatingItem.fromJson(Map<String, dynamic> json) {
    return DriverRatingItem(
      id: (json['id'] as num?)?.toInt() ?? 0,
      tripId: (json['tripId'] as num?)?.toInt() ?? 0,
      customerName: json['customerName']?.toString() ?? '',
      customerAvatarUrl: json['customerAvatarUrl']?.toString(),
      score: (json['score'] as num?)?.toInt() ?? 0,
      comment: json['comment']?.toString(),
      createdAt: json['createdAt'] == null
          ? DateTime.now()
          : DateTime.tryParse(json['createdAt'].toString()) ?? DateTime.now(),
    );
  }
}

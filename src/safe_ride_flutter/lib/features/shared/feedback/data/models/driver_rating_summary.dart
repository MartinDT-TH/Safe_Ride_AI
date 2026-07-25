import 'driver_rating_item.dart';

class DriverRatingSummary {
  final String driverId;
  final double averageRating;
  final int totalRatings;
  final List<DriverRatingItem> ratings;

  const DriverRatingSummary({
    required this.driverId,
    required this.averageRating,
    required this.totalRatings,
    required this.ratings,
  });

  factory DriverRatingSummary.fromJson(Map<String, dynamic> json) {
    final ratingsList = json['ratings'] as List?;
    return DriverRatingSummary(
      driverId: json['driverId']?.toString() ?? '',
      averageRating: (json['averageRating'] as num?)?.toDouble() ?? 0.0,
      totalRatings: (json['totalRatings'] as num?)?.toInt() ?? 0,
      ratings: ratingsList != null
          ? ratingsList
              .map((item) => DriverRatingItem.fromJson(item as Map<String, dynamic>))
              .toList()
          : [],
    );
  }
}

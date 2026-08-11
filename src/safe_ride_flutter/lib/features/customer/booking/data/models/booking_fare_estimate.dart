import '../../../../../core/constants/app_strings.dart';

class BookingFareEstimate {
  const BookingFareEstimate({
    required this.estimatedDistanceKm,
    required this.estimatedDurationMinutes,
    required this.encodedPolyline,
    required this.estimatedFare,
    this.surgeMultiplier,
  });

  final double estimatedDistanceKm;
  final int estimatedDurationMinutes;
  final String encodedPolyline;
  final double estimatedFare;
  final double? surgeMultiplier;

  factory BookingFareEstimate.fromJson(Map<String, dynamic> json) {
    final estimatedFareValue = json[ApiKeys.estimatedFare];
    if (estimatedFareValue is! num ||
        !estimatedFareValue.toDouble().isFinite ||
        estimatedFareValue.toDouble() <= 0) {
      throw const FormatException('Invalid estimated fare response.');
    }

    return BookingFareEstimate(
      estimatedDistanceKm:
          (json[ApiKeys.estimatedDistanceKm] as num?)?.toDouble() ?? 0,
      estimatedDurationMinutes:
          (json[ApiKeys.estimatedDurationMinutes] as num?)?.toInt() ?? 0,
      encodedPolyline: json[ApiKeys.encodedPolyline]?.toString() ?? '',
      estimatedFare: estimatedFareValue.toDouble(),
      surgeMultiplier: (json['surgeMultiplier'] as num?)?.toDouble(),
    );
  }
}

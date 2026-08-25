import '../../../../../core/constants/app_strings.dart';

class BookingFareEstimate {
  const BookingFareEstimate({
    required this.estimatedDistanceKm,
    required this.estimatedDurationMinutes,
    required this.encodedPolyline,
    required this.estimatedFare,
    required this.normalFare,
    required this.surgedFare,
    required this.surgeAmount,
    required this.longDistanceComponent,
    required this.minimumServiceFare,
    this.surgeMultiplier,
  });

  final double estimatedDistanceKm;
  final int estimatedDurationMinutes;
  final String encodedPolyline;
  final double estimatedFare;
  final double normalFare;
  final double surgedFare;
  final double surgeAmount;
  final double longDistanceComponent;
  final double minimumServiceFare;
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
      normalFare:
          (json['normalFare'] as num?)?.toDouble() ??
          estimatedFareValue.toDouble(),
      surgedFare:
          (json['surgedFare'] as num?)?.toDouble() ??
          estimatedFareValue.toDouble(),
      surgeAmount: (json['surgeAmount'] as num?)?.toDouble() ?? 0,
      longDistanceComponent:
          (json['longDistanceComponent'] as num?)?.toDouble() ?? 0,
      minimumServiceFare:
          (json['minimumServiceFare'] as num?)?.toDouble() ?? 0,
      surgeMultiplier: (json['surgeMultiplier'] as num?)?.toDouble(),
    );
  }
}

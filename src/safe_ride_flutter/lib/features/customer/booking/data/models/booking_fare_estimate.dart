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
    return BookingFareEstimate(
      estimatedDistanceKm:
          (json[ApiKeys.estimatedDistanceKm] as num?)?.toDouble() ?? 0,
      estimatedDurationMinutes:
          (json[ApiKeys.estimatedDurationMinutes] as num?)?.toInt() ?? 0,
      encodedPolyline: json[ApiKeys.encodedPolyline]?.toString() ?? '',
      estimatedFare: (json[ApiKeys.estimatedFare] as num?)?.toDouble() ?? 0,
      surgeMultiplier: (json['surgeMultiplier'] as num?)?.toDouble(),
    );
  }
}


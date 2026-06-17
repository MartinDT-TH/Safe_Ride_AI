import '../../../../../core/constants/app_strings.dart';

class BookingResponse {
  const BookingResponse({
    required this.bookingId,
    required this.bookingType,
    required this.bookingStatus,
    required this.estimatedDistanceKm,
    required this.estimatedDurationMinutes,
    required this.estimatedFare,
    required this.encodedPolyline,
    required this.message,
    this.scheduledAt,
  });

  final int bookingId;
  final String bookingType;
  final String bookingStatus;
  final DateTime? scheduledAt;
  final double estimatedDistanceKm;
  final int estimatedDurationMinutes;
  final double estimatedFare;
  final String encodedPolyline;
  final String message;

  factory BookingResponse.fromJson(Map<String, dynamic> json) {
    return BookingResponse(
      bookingId: (json[ApiKeys.bookingId] as num).toInt(),
      bookingType: json[ApiKeys.bookingType]?.toString() ?? '',
      bookingStatus: json[ApiKeys.bookingStatus]?.toString() ?? '',
      scheduledAt: json[ApiKeys.scheduledAt] == null
          ? null
          : DateTime.tryParse(json[ApiKeys.scheduledAt].toString()),
      estimatedDistanceKm:
          (json[ApiKeys.estimatedDistanceKm] as num?)?.toDouble() ?? 0,
      estimatedDurationMinutes:
          (json[ApiKeys.estimatedDurationMinutes] as num?)?.toInt() ?? 0,
      estimatedFare: (json[ApiKeys.estimatedFare] as num?)?.toDouble() ?? 0,
      encodedPolyline: json[ApiKeys.encodedPolyline]?.toString() ?? '',
      message:
          json[ApiKeys.message]?.toString() ?? BookingStrings.bookingSuccess,
    );
  }
}

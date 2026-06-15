import '../../../../core/constants/app_strings.dart';

class BookingResponse {
  const BookingResponse({
    required this.bookingId,
    required this.bookingType,
    required this.bookingStatus,
    required this.estimatedFare,
    required this.message,
    this.scheduledAt,
  });

  final int bookingId;
  final String bookingType;
  final String bookingStatus;
  final DateTime? scheduledAt;
  final double estimatedFare;
  final String message;

  factory BookingResponse.fromJson(Map<String, dynamic> json) {
    return BookingResponse(
      bookingId: (json[ApiKeys.bookingId] as num).toInt(),
      bookingType: json[ApiKeys.bookingType]?.toString() ?? '',
      bookingStatus: json[ApiKeys.bookingStatus]?.toString() ?? '',
      scheduledAt: json[ApiKeys.scheduledAt] == null
          ? null
          : DateTime.tryParse(json[ApiKeys.scheduledAt].toString()),
      estimatedFare: (json[ApiKeys.estimatedFare] as num?)?.toDouble() ?? 0,
      message:
          json[ApiKeys.message]?.toString() ?? BookingStrings.bookingSuccess,
    );
  }
}

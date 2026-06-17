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
    this.driverOffer,
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
  final BookingDriverOffer? driverOffer;

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
      driverOffer: json[ApiKeys.driverOffer] is Map
          ? BookingDriverOffer.fromJson(
              Map<String, dynamic>.from(json[ApiKeys.driverOffer] as Map),
            )
          : null,
    );
  }
}

class BookingDriverOffer {
  const BookingDriverOffer({
    required this.offerId,
    required this.driverId,
    required this.driverName,
    required this.rating,
    required this.tripCount,
    required this.experienceYears,
    required this.licenseClass,
    required this.expiresAt,
    this.driverAvatarUrl,
  });

  final int offerId;
  final String driverId;
  final String driverName;
  final String? driverAvatarUrl;
  final double rating;
  final int tripCount;
  final int experienceYears;
  final String licenseClass;
  final DateTime? expiresAt;

  factory BookingDriverOffer.fromJson(Map<String, dynamic> json) {
    return BookingDriverOffer(
      offerId: (json[ApiKeys.offerId] as num?)?.toInt() ?? 0,
      driverId: json[ApiKeys.driverId]?.toString() ?? '',
      driverName: json[ApiKeys.driverName]?.toString() ?? 'Tài xế SafeRide',
      driverAvatarUrl: json[ApiKeys.driverAvatarUrl]?.toString(),
      rating: (json[ApiKeys.rating] as num?)?.toDouble() ?? 0,
      tripCount: (json[ApiKeys.tripCount] as num?)?.toInt() ?? 0,
      experienceYears: (json[ApiKeys.experienceYears] as num?)?.toInt() ?? 0,
      licenseClass: json[ApiKeys.licenseClass]?.toString() ?? '',
      expiresAt: json[ApiKeys.expiresAt] == null
          ? null
          : DateTime.tryParse(json[ApiKeys.expiresAt].toString()),
    );
  }
}

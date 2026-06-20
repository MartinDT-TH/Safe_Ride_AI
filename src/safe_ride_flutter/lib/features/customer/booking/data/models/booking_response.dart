import '../../../../../core/constants/app_strings.dart';
import 'booking_catalog.dart';
import 'booking_location.dart';

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
    this.arrivalPolyline,
    this.scheduledAt,
    this.driverOffer,
    this.pickup,
    this.destination,
    this.vehicle,
    this.tripId,
    this.tripStatus,
    this.originalFare,
    this.promotionCode,
    this.discountAmount,
    this.finalFare,
  });

  final int bookingId;
  final String bookingType;
  final String bookingStatus;
  final DateTime? scheduledAt;
  final double estimatedDistanceKm;
  final int estimatedDurationMinutes;
  final double estimatedFare;
  final String encodedPolyline;
  final String? arrivalPolyline;
  final String message;
  final BookingDriverOffer? driverOffer;
  final BookingLocation? pickup;
  final BookingLocation? destination;
  final BookingVehicleOption? vehicle;
  final int? tripId;
  final String? tripStatus;
  final double? originalFare;
  final String? promotionCode;
  final double? discountAmount;
  final double? finalFare;

  factory BookingResponse.fromJson(Map<String, dynamic> json) {
    final estimatedFareValue = (json[ApiKeys.estimatedFare] as num?)?.toDouble() ?? 0;
    final originalFareFromApi = (json[ApiKeys.originalFare] as num?)?.toDouble();
    final discountAmountValue = (json[ApiKeys.discountAmount] as num?)?.toDouble() ?? 0;
    final finalFareFromApi = (json[ApiKeys.finalFare] as num?)?.toDouble();

    // Fallback originalFare to estimatedFare if it's null or zero
    final originalFareValue = (originalFareFromApi == null || originalFareFromApi == 0)
        ? estimatedFareValue
        : originalFareFromApi;

    // Calculate final fare.
    // If we have a discount (> 0), we prioritize calculating it (original - discount)
    // especially if the backend returns the full price as finalFare or returns 0.
    var calculatedFinalFare = finalFareFromApi;

    if (discountAmountValue > 0) {
      final expectedFare = originalFareValue - discountAmountValue;
      // If finalFare is missing, zero, or matches the original price despite having a discount, recalculate it.
      if (calculatedFinalFare == null ||
          calculatedFinalFare == 0 ||
          (calculatedFinalFare - originalFareValue).abs() < 1.0 ||
          (calculatedFinalFare - estimatedFareValue).abs() < 1.0) {
        calculatedFinalFare = expectedFare;
      }
    } else {
      // No discount applied in this JSON
      if (calculatedFinalFare == null || calculatedFinalFare == 0) {
        calculatedFinalFare = estimatedFareValue;
      }
    }

    // Ensure finalFare is never negative
    if (calculatedFinalFare < 0) calculatedFinalFare = 0;

    return BookingResponse(
      bookingId: (json[ApiKeys.bookingId] as num?)?.toInt() ?? 0,
      bookingType: json[ApiKeys.bookingType]?.toString() ?? '',
      bookingStatus: json[ApiKeys.bookingStatus]?.toString() ?? '',
      scheduledAt: json[ApiKeys.scheduledAt] == null
          ? null
          : DateTime.tryParse(json[ApiKeys.scheduledAt].toString()),
      estimatedDistanceKm:
          (json[ApiKeys.estimatedDistanceKm] as num?)?.toDouble() ?? 0,
      estimatedDurationMinutes:
          (json[ApiKeys.estimatedDurationMinutes] as num?)?.toInt() ?? 0,
      estimatedFare: estimatedFareValue,
      encodedPolyline: json[ApiKeys.encodedPolyline]?.toString() ?? '',
      arrivalPolyline: json[ApiKeys.arrivalPolyline]?.toString(),
      message:
          json[ApiKeys.message]?.toString() ?? BookingStrings.bookingSuccess,
      driverOffer: json[ApiKeys.driverOffer] is Map
          ? BookingDriverOffer.fromJson(
              Map<String, dynamic>.from(json[ApiKeys.driverOffer] as Map),
            )
          : null,
      pickup: json[ApiKeys.pickup] is Map
          ? _locationFromJson(
              Map<String, dynamic>.from(json[ApiKeys.pickup] as Map),
            )
          : null,
      destination: json[ApiKeys.destination] is Map
          ? _locationFromJson(
              Map<String, dynamic>.from(json[ApiKeys.destination] as Map),
            )
          : null,
      vehicle: json[ApiKeys.vehicle] is Map
          ? BookingVehicleOption.fromJson(
              Map<String, dynamic>.from(json[ApiKeys.vehicle] as Map),
            )
          : null,
      tripId: (json[ApiKeys.tripId] as num?)?.toInt(),
      tripStatus: json[ApiKeys.tripStatus]?.toString(),
      originalFare: originalFareValue,
      promotionCode: json[ApiKeys.promotionCode]?.toString(),
      discountAmount: discountAmountValue,
      finalFare: calculatedFinalFare,
    );
  }

  BookingResponse copyWith({
    int? bookingId,
    String? bookingType,
    String? bookingStatus,
    DateTime? scheduledAt,
    double? estimatedDistanceKm,
    int? estimatedDurationMinutes,
    double? estimatedFare,
    String? encodedPolyline,
    String? arrivalPolyline,
    String? message,
    BookingDriverOffer? driverOffer,
    BookingLocation? pickup,
    BookingLocation? destination,
    BookingVehicleOption? vehicle,
    int? tripId,
    String? tripStatus,
    double? originalFare,
    String? promotionCode,
    double? discountAmount,
    double? finalFare,
  }) {
    return BookingResponse(
      bookingId: bookingId ?? this.bookingId,
      bookingType: bookingType ?? this.bookingType,
      bookingStatus: bookingStatus ?? this.bookingStatus,
      scheduledAt: scheduledAt ?? this.scheduledAt,
      estimatedDistanceKm: estimatedDistanceKm ?? this.estimatedDistanceKm,
      estimatedDurationMinutes:
          estimatedDurationMinutes ?? this.estimatedDurationMinutes,
      estimatedFare: estimatedFare ?? this.estimatedFare,
      encodedPolyline: encodedPolyline ?? this.encodedPolyline,
      arrivalPolyline: arrivalPolyline ?? this.arrivalPolyline,
      message: message ?? this.message,
      driverOffer: driverOffer ?? this.driverOffer,
      pickup: pickup ?? this.pickup,
      destination: destination ?? this.destination,
      vehicle: vehicle ?? this.vehicle,
      tripId: tripId ?? this.tripId,
      tripStatus: tripStatus ?? this.tripStatus,
      originalFare: originalFare ?? this.originalFare,
      promotionCode: promotionCode ?? this.promotionCode,
      discountAmount: discountAmount ?? this.discountAmount,
      finalFare: finalFare ?? this.finalFare,
    );
  }

  BookingResponse mergeWithPreservedPromotion(BookingResponse newer) {
    // Determine if either version has promotion information
    final bool oldHasPromo = (promotionCode != null && promotionCode!.trim().isNotEmpty) ||
                             (discountAmount != null && discountAmount! > 0);

    final bool newHasPromo = (newer.promotionCode != null && newer.promotionCode!.trim().isNotEmpty) ||
                             (newer.discountAmount != null && newer.discountAmount! > 0);

    // Case 1: Newer response is completely missing promotion info (typical polling)
    if (oldHasPromo && !newHasPromo) {
      final double preservedOriginalFare = (newer.originalFare != null && newer.originalFare! > 0)
          ? newer.originalFare!
          : (originalFare ?? newer.estimatedFare);

      final double preservedDiscount = discountAmount ?? 0;
      final String? preservedCode = promotionCode;

      var calculatedFinalFare = preservedOriginalFare - preservedDiscount;
      if (calculatedFinalFare < 0) calculatedFinalFare = 0;

      return newer.copyWith(
        promotionCode: preservedCode,
        discountAmount: preservedDiscount,
        originalFare: preservedOriginalFare,
        finalFare: calculatedFinalFare,
      );
    }

    // Case 2: Newer response has promotion info (e.g. detailed get booking or fresh create)
    // We still want to be careful about finalFare if it looks like the original price
    if (newHasPromo) {
      final double newerOriginal = newer.originalFare ?? newer.estimatedFare;
      final double newerDiscount = newer.discountAmount ?? 0;
      var newerFinal = newer.finalFare ?? 0;

      // If newerFinal is suspiciously equal to original price despite having a discount
      if (newerDiscount > 0 &&
          (newerFinal == 0 ||
           (newerFinal - newerOriginal).abs() < 1.0 ||
           (newerFinal - newer.estimatedFare).abs() < 1.0)) {
        newerFinal = newerOriginal - newerDiscount;
      }

      if (newerFinal < 0) newerFinal = 0;

      return newer.copyWith(finalFare: newerFinal);
    }

    // Default fallback
    return newer;
  }

  static BookingLocation _locationFromJson(Map<String, dynamic> json) {
    return BookingLocation(
      address: json[ApiKeys.address]?.toString() ?? '',
      latitude: (json[ApiKeys.latitude] as num?)?.toDouble() ?? 0,
      longitude: (json[ApiKeys.longitude] as num?)?.toDouble() ?? 0,
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

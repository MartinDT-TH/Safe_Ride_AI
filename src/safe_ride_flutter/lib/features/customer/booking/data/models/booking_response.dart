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
    this.currentSearchRadiusKm,
    this.expiresAt,
    this.estimatedRemainingSeconds,
    this.matchingMessage,
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
  final double? currentSearchRadiusKm;
  final DateTime? expiresAt;
  final int? estimatedRemainingSeconds;
  final String? matchingMessage;

  factory BookingResponse.fromJson(Map<String, dynamic> json) {
    final estimatedFareValue =
        (_value(json, ApiKeys.estimatedFare) as num?)?.toDouble() ?? 0;
    final originalFareFromApi =
        (_value(json, ApiKeys.originalFare) as num?)?.toDouble();
    final discountAmountValue =
        (_value(json, ApiKeys.discountAmount) as num?)?.toDouble() ?? 0;
    final finalFareFromApi =
        (_value(json, ApiKeys.finalFare) as num?)?.toDouble();
    final driverOfferRaw = _value(json, ApiKeys.driverOffer);
    final pickupRaw = _value(json, ApiKeys.pickup);
    final destinationRaw = _value(json, ApiKeys.destination);
    final vehicleRaw = _value(json, ApiKeys.vehicle);

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
      bookingId: (_value(json, ApiKeys.bookingId) as num?)?.toInt() ?? 0,
      bookingType: _value(json, ApiKeys.bookingType)?.toString() ?? '',
      bookingStatus:
          _normalizeBookingStatus(_value(json, ApiKeys.bookingStatus)) ?? '',
      scheduledAt: _value(json, ApiKeys.scheduledAt) == null
          ? null
          : DateTime.tryParse(_value(json, ApiKeys.scheduledAt).toString()),
      estimatedDistanceKm:
          (_value(json, ApiKeys.estimatedDistanceKm) as num?)?.toDouble() ?? 0,
      estimatedDurationMinutes:
          (_value(json, ApiKeys.estimatedDurationMinutes) as num?)?.toInt() ??
              0,
      estimatedFare: estimatedFareValue,
      encodedPolyline: _value(json, ApiKeys.encodedPolyline)?.toString() ?? '',
      arrivalPolyline: _value(json, ApiKeys.arrivalPolyline)?.toString(),
      message:
          _value(json, ApiKeys.message)?.toString() ?? BookingStrings.bookingSuccess,
      driverOffer: driverOfferRaw is Map
          ? BookingDriverOffer.fromJson(
              Map<String, dynamic>.from(driverOfferRaw),
            )
          : null,
      pickup: pickupRaw is Map
          ? _locationFromJson(
              Map<String, dynamic>.from(pickupRaw),
            )
          : null,
      destination: destinationRaw is Map
          ? _locationFromJson(
              Map<String, dynamic>.from(destinationRaw),
            )
          : null,
      vehicle: vehicleRaw is Map
          ? BookingVehicleOption.fromJson(
              Map<String, dynamic>.from(vehicleRaw),
            )
          : null,
      tripId: (_value(json, ApiKeys.tripId) as num?)?.toInt(),
      tripStatus: _normalizeTripStatus(_value(json, ApiKeys.tripStatus)),
      originalFare: originalFareValue,
      promotionCode: _value(json, ApiKeys.promotionCode)?.toString(),
      discountAmount: discountAmountValue,
      finalFare: calculatedFinalFare,
      currentSearchRadiusKm:
          (_value(json, ApiKeys.currentSearchRadiusKm) as num?)?.toDouble(),
      expiresAt: _value(json, ApiKeys.expiresAt) == null
          ? null
          : DateTime.tryParse(_value(json, ApiKeys.expiresAt).toString()),
      estimatedRemainingSeconds:
          (_value(json, ApiKeys.estimatedRemainingSeconds) as num?)?.toInt(),
      matchingMessage: _value(json, ApiKeys.matchingMessage)?.toString(),
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
    double? currentSearchRadiusKm,
    DateTime? expiresAt,
    int? estimatedRemainingSeconds,
    String? matchingMessage,
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
      currentSearchRadiusKm:
          currentSearchRadiusKm ?? this.currentSearchRadiusKm,
      expiresAt: expiresAt ?? this.expiresAt,
      estimatedRemainingSeconds:
          estimatedRemainingSeconds ?? this.estimatedRemainingSeconds,
      matchingMessage: matchingMessage ?? this.matchingMessage,
    );
  }

  BookingResponse mergeWithPreservedPromotion(BookingResponse newer) {
    // Determine if either version has promotion information
    final bool oldHasPromo = (promotionCode != null && promotionCode!.trim().isNotEmpty) ||
                             (discountAmount != null && discountAmount! > 0);

    final bool newHasPromo = (newer.promotionCode != null && newer.promotionCode!.trim().isNotEmpty) ||
                             (newer.discountAmount != null && newer.discountAmount! > 0);

    // Preserve polylines if missing in newer response
    String? preservedEncodedPolyline = newer.encodedPolyline;
    if (preservedEncodedPolyline.isEmpty && encodedPolyline.isNotEmpty) {
      preservedEncodedPolyline = encodedPolyline;
    }

    String? preservedArrivalPolyline = newer.arrivalPolyline;
    if ((preservedArrivalPolyline == null || preservedArrivalPolyline.isEmpty) &&
        (arrivalPolyline != null && arrivalPolyline!.isNotEmpty)) {
      preservedArrivalPolyline = arrivalPolyline;
    }

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
        encodedPolyline: preservedEncodedPolyline,
        arrivalPolyline: preservedArrivalPolyline,
        pickup: newer.pickup ?? pickup,
        destination: newer.destination ?? destination,
        vehicle: newer.vehicle ?? vehicle,
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

      return newer.copyWith(
        finalFare: newerFinal,
        encodedPolyline: preservedEncodedPolyline,
        arrivalPolyline: preservedArrivalPolyline,
        pickup: newer.pickup ?? pickup,
        destination: newer.destination ?? destination,
        vehicle: newer.vehicle ?? vehicle,
      );
    }

    // Default fallback - still preserve polylines and other critical fields
    return newer.copyWith(
      encodedPolyline: preservedEncodedPolyline,
      arrivalPolyline: preservedArrivalPolyline,
      pickup: newer.pickup ?? pickup,
      destination: newer.destination ?? destination,
      vehicle: newer.vehicle ?? vehicle,
    );
  }

  static BookingLocation _locationFromJson(Map<String, dynamic> json) {
    return BookingLocation(
      address: _value(json, ApiKeys.address)?.toString() ?? '',
      latitude: (_value(json, ApiKeys.latitude) as num?)?.toDouble() ?? 0,
      longitude: (_value(json, ApiKeys.longitude) as num?)?.toDouble() ?? 0,
    );
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey =
        key.isEmpty ? key : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }

  static String? _normalizeBookingStatus(Object? value) {
    if (value == null) return null;
    if (value is num) {
      return switch (value.toInt()) {
        0 => 'PendingSchedule',
        1 => 'Searching',
        2 => 'DriverAssigned',
        3 => 'Cancelled',
        4 => 'Expired',
        5 => 'Completed',
        _ => value.toString(),
      };
    }

    final text = value.toString();
    return switch (text) {
      '0' => 'PendingSchedule',
      '1' => 'Searching',
      '2' => 'DriverAssigned',
      '3' => 'Cancelled',
      '4' => 'Expired',
      '5' => 'Completed',
      _ => text,
    };
  }

  static String? _normalizeTripStatus(Object? value) {
    if (value == null) return null;
    if (value is num) {
      return switch (value.toInt()) {
        0 => 'ACCEPTED',
        1 => 'DRIVER_ARRIVING',
        2 => 'ARRIVED',
        3 => 'IN_PROGRESS',
        4 => 'COMPLETED',
        5 => 'CANCELLED',
        _ => value.toString(),
      };
    }

    final text = value.toString();
    return switch (text) {
      '0' => 'ACCEPTED',
      '1' => 'DRIVER_ARRIVING',
      '2' => 'ARRIVED',
      '3' => 'IN_PROGRESS',
      '4' => 'COMPLETED',
      '5' => 'CANCELLED',
      _ => text,
    };
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
    this.offerStatus,
    this.customerConfirmRemainingSeconds,
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
  final String? offerStatus;
  final int? customerConfirmRemainingSeconds;

  factory BookingDriverOffer.fromJson(Map<String, dynamic> json) {
    return BookingDriverOffer(
      offerId: (_value(json, ApiKeys.offerId) as num?)?.toInt() ?? 0,
      driverId: _value(json, ApiKeys.driverId)?.toString() ?? '',
      driverName: _value(json, ApiKeys.driverName)?.toString() ?? 'Tai xe SafeRide',
      driverAvatarUrl: _value(json, ApiKeys.driverAvatarUrl)?.toString(),
      rating: (_value(json, ApiKeys.rating) as num?)?.toDouble() ?? 0,
      tripCount: (_value(json, ApiKeys.tripCount) as num?)?.toInt() ?? 0,
      experienceYears:
          (_value(json, ApiKeys.experienceYears) as num?)?.toInt() ?? 0,
      licenseClass: _value(json, ApiKeys.licenseClass)?.toString() ?? '',
      expiresAt: _value(json, ApiKeys.expiresAt) == null
          ? null
          : DateTime.tryParse(_value(json, ApiKeys.expiresAt).toString()),
      offerStatus: _normalizeOfferStatus(_value(json, ApiKeys.offerStatus)),
      customerConfirmRemainingSeconds:
          (_value(json, ApiKeys.customerConfirmRemainingSeconds) as num?)
              ?.toInt(),
    );
  }

  static String? _normalizeOfferStatus(Object? value) {
    if (value == null) return null;
    if (value is num) {
      return switch (value.toInt()) {
        0 => 'Sent',
        1 => 'DriverAccepted',
        2 => 'CustomerConfirmed',
        3 => 'Rejected',
        4 => 'Expired',
        5 => 'Cancelled',
        _ => value.toString(),
      };
    }

    final text = value.toString();
    return switch (text) {
      '0' => 'Sent',
      '1' => 'DriverAccepted',
      '2' => 'CustomerConfirmed',
      '3' => 'Rejected',
      '4' => 'Expired',
      '5' => 'Cancelled',
      _ => text,
    };
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey =
        key.isEmpty ? key : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }
}

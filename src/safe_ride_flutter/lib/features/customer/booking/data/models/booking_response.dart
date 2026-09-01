import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/localization/locale_provider.dart';
import '../../../../../core/utils/api_date_time.dart';
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
    this.actualDistanceKm,
    this.actualDurationMinutes,
    this.actualEncodedPolyline,
    this.tripEndedAt,
    this.terminationCategory,
    this.safetyTerminationReason,
    this.safetyTerminatedAt,
    this.scheduledAt,
    this.driverOffer,
    this.pickup,
    this.destination,
    this.vehicle,
    this.tripId,
    this.tripStatus,
    this.isSOSActivated = false,
    this.originalFare,
    this.promotionCode,
    this.discountAmount,
    this.finalFare,
    this.currentSearchRadiusKm,
    this.expiresAt,
    this.estimatedRemainingSeconds,
    this.matchingMessage,
    this.payment,
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
  final double? actualDistanceKm;
  final int? actualDurationMinutes;
  final String? actualEncodedPolyline;
  final DateTime? tripEndedAt;
  final String? terminationCategory;
  final String? safetyTerminationReason;
  final DateTime? safetyTerminatedAt;
  final String message;
  final BookingDriverOffer? driverOffer;
  final BookingLocation? pickup;
  final BookingLocation? destination;
  final BookingVehicleOption? vehicle;
  final int? tripId;
  final String? tripStatus;
  final bool isSOSActivated;
  final double? originalFare;
  final String? promotionCode;
  final double? discountAmount;
  final double? finalFare;
  final double? currentSearchRadiusKm;
  final DateTime? expiresAt;
  final int? estimatedRemainingSeconds;
  final String? matchingMessage;
  final TripPaymentSummary? payment;

  bool get isSearchingNowBooking =>
      bookingType == AppValues.bookingNow && bookingStatus == 'Searching';

  bool get isTrackableTrip =>
      bookingStatus == 'DriverAssigned' && tripId != null;

  bool get isSafetyTerminated =>
      tripStatus == 'CANCELLED' &&
      terminationCategory?.trim().toUpperCase() == 'SAFETY';

  factory BookingResponse.fromJson(Map<String, dynamic> json) {
    final estimatedFareValue =
        (_value(json, ApiKeys.estimatedFare) as num?)?.toDouble() ?? 0;
    final originalFareFromApi = (_value(json, ApiKeys.originalFare) as num?)
        ?.toDouble();
    final discountAmountValue =
        (_value(json, ApiKeys.discountAmount) as num?)?.toDouble() ?? 0;
    final finalFareFromApi = (_value(json, ApiKeys.finalFare) as num?)
        ?.toDouble();
    final driverOfferRaw = _value(json, ApiKeys.driverOffer);
    final pickupRaw = _value(json, ApiKeys.pickup);
    final destinationRaw = _value(json, ApiKeys.destination);
    final vehicleRaw = _value(json, ApiKeys.vehicle);
    final paymentRaw = _value(json, ApiKeys.payment);

    // Explicit zero is authoritative for a finalized trip with no usage.
    final originalFareValue = originalFareFromApi ?? estimatedFareValue;

    // Only calculate a fallback when the backend omitted finalFare. Zero is valid.
    var calculatedFinalFare = finalFareFromApi;

    if (calculatedFinalFare == null) {
      calculatedFinalFare = discountAmountValue > 0
          ? originalFareValue - discountAmountValue
          : estimatedFareValue;
    }

    // Ensure finalFare is never negative
    if (calculatedFinalFare < 0) calculatedFinalFare = 0;

    return BookingResponse(
      bookingId: (_value(json, ApiKeys.bookingId) as num?)?.toInt() ?? 0,
      bookingType: _value(json, ApiKeys.bookingType)?.toString() ?? '',
      bookingStatus:
          _normalizeBookingStatus(_value(json, ApiKeys.bookingStatus)) ?? '',
      scheduledAt: parseApiUtcDateTimeToLocal(
        _value(json, ApiKeys.scheduledAt),
      ),
      estimatedDistanceKm:
          (_value(json, ApiKeys.estimatedDistanceKm) as num?)?.toDouble() ?? 0,
      estimatedDurationMinutes:
          (_value(json, ApiKeys.estimatedDurationMinutes) as num?)?.toInt() ??
          0,
      estimatedFare: estimatedFareValue,
      encodedPolyline: _value(json, ApiKeys.encodedPolyline)?.toString() ?? '',
      arrivalPolyline: _value(json, ApiKeys.arrivalPolyline)?.toString(),
      actualDistanceKm: (_value(json, ApiKeys.actualDistanceKm) as num?)
          ?.toDouble(),
      actualDurationMinutes:
          (_value(json, ApiKeys.actualDurationMinutes) as num?)?.toInt(),
      actualEncodedPolyline: _value(
        json,
        ApiKeys.actualEncodedPolyline,
      )?.toString(),
      tripEndedAt: _value(json, ApiKeys.tripEndedAt) == null
          ? null
          : DateTime.tryParse(_value(json, ApiKeys.tripEndedAt).toString()),
      terminationCategory: _value(
        json,
        ApiKeys.terminationCategory,
      )?.toString(),
      safetyTerminationReason: _value(
        json,
        ApiKeys.safetyTerminationReason,
      )?.toString(),
      safetyTerminatedAt: parseApiUtcDateTimeToLocal(
        _value(json, ApiKeys.safetyTerminatedAt),
      ),
      message:
          _value(json, ApiKeys.message)?.toString() ??
          LocaleProvider.currentLocalizations.bookingSuccess,
      driverOffer: driverOfferRaw is Map
          ? BookingDriverOffer.fromJson(
              Map<String, dynamic>.from(driverOfferRaw),
            )
          : null,
      pickup: pickupRaw is Map
          ? _locationFromJson(Map<String, dynamic>.from(pickupRaw))
          : null,
      destination: destinationRaw is Map
          ? _locationFromJson(Map<String, dynamic>.from(destinationRaw))
          : null,
      vehicle: vehicleRaw is Map
          ? BookingVehicleOption.fromJson(Map<String, dynamic>.from(vehicleRaw))
          : null,
      tripId: (_value(json, ApiKeys.tripId) as num?)?.toInt(),
      tripStatus: _normalizeTripStatus(_value(json, ApiKeys.tripStatus)),
      isSOSActivated:
          _value(json, ApiKeys.isSOSActivated)?.toString().toLowerCase() ==
          'true',
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
      payment: paymentRaw is Map
          ? TripPaymentSummary.fromJson(Map<String, dynamic>.from(paymentRaw))
          : null,
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
    double? actualDistanceKm,
    int? actualDurationMinutes,
    String? actualEncodedPolyline,
    DateTime? tripEndedAt,
    String? terminationCategory,
    String? safetyTerminationReason,
    DateTime? safetyTerminatedAt,
    String? message,
    BookingDriverOffer? driverOffer,
    BookingLocation? pickup,
    BookingLocation? destination,
    BookingVehicleOption? vehicle,
    int? tripId,
    String? tripStatus,
    bool? isSOSActivated,
    double? originalFare,
    String? promotionCode,
    double? discountAmount,
    double? finalFare,
    double? currentSearchRadiusKm,
    DateTime? expiresAt,
    int? estimatedRemainingSeconds,
    String? matchingMessage,
    TripPaymentSummary? payment,
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
      actualDistanceKm: actualDistanceKm ?? this.actualDistanceKm,
      actualDurationMinutes:
          actualDurationMinutes ?? this.actualDurationMinutes,
      actualEncodedPolyline:
          actualEncodedPolyline ?? this.actualEncodedPolyline,
      tripEndedAt: tripEndedAt ?? this.tripEndedAt,
      terminationCategory: terminationCategory ?? this.terminationCategory,
      safetyTerminationReason:
          safetyTerminationReason ?? this.safetyTerminationReason,
      safetyTerminatedAt: safetyTerminatedAt ?? this.safetyTerminatedAt,
      message: message ?? this.message,
      driverOffer: driverOffer ?? this.driverOffer,
      pickup: pickup ?? this.pickup,
      destination: destination ?? this.destination,
      vehicle: vehicle ?? this.vehicle,
      tripId: tripId ?? this.tripId,
      tripStatus: tripStatus ?? this.tripStatus,
      isSOSActivated: isSOSActivated ?? this.isSOSActivated,
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
      payment: payment ?? this.payment,
    );
  }

  BookingResponse mergeWithPreservedPromotion(BookingResponse newer) {
    // Determine if either version has promotion information
    final bool oldHasPromo =
        (promotionCode != null && promotionCode!.trim().isNotEmpty) ||
        (discountAmount != null && discountAmount! > 0);

    final bool newHasPromo =
        (newer.promotionCode != null &&
            newer.promotionCode!.trim().isNotEmpty) ||
        (newer.discountAmount != null && newer.discountAmount! > 0);

    // Preserve polylines if missing in newer response
    String? preservedEncodedPolyline = newer.encodedPolyline;
    if (preservedEncodedPolyline.isEmpty && encodedPolyline.isNotEmpty) {
      preservedEncodedPolyline = encodedPolyline;
    }

    String? preservedArrivalPolyline = newer.arrivalPolyline;
    if ((preservedArrivalPolyline == null ||
            preservedArrivalPolyline.isEmpty) &&
        (arrivalPolyline != null && arrivalPolyline!.isNotEmpty)) {
      preservedArrivalPolyline = arrivalPolyline;
    }

    String? preservedActualEncodedPolyline = newer.actualEncodedPolyline;
    if ((preservedActualEncodedPolyline == null ||
            preservedActualEncodedPolyline.isEmpty) &&
        (actualEncodedPolyline != null && actualEncodedPolyline!.isNotEmpty)) {
      preservedActualEncodedPolyline = actualEncodedPolyline;
    }

    // A safety termination explicitly releases the promotion. Do not merge
    // stale promotion data from the previously active booking into the
    // cancelled response or its partial fare.
    if (newer.isSafetyTerminated) {
      return newer.copyWith(
        encodedPolyline: preservedEncodedPolyline,
        arrivalPolyline: preservedArrivalPolyline,
        actualDistanceKm: newer.actualDistanceKm ?? actualDistanceKm,
        actualDurationMinutes:
            newer.actualDurationMinutes ?? actualDurationMinutes,
        actualEncodedPolyline: preservedActualEncodedPolyline,
        tripEndedAt: newer.tripEndedAt ?? tripEndedAt,
        pickup: newer.pickup ?? pickup,
        destination: newer.destination ?? destination,
        vehicle: newer.vehicle ?? vehicle,
        payment: newer.payment ?? payment,
      );
    }

    // Case 1: Newer response is completely missing promotion info (typical polling)
    if (oldHasPromo && !newHasPromo) {
      final double preservedOriginalFare =
          newer.originalFare ?? originalFare ?? newer.estimatedFare;

      final double preservedDiscount = discountAmount ?? 0;
      final String? preservedCode = promotionCode;

      var calculatedFinalFare =
          newer.finalFare ?? preservedOriginalFare - preservedDiscount;
      if (calculatedFinalFare < 0) calculatedFinalFare = 0;

      return newer.copyWith(
        promotionCode: preservedCode,
        discountAmount: preservedDiscount,
        originalFare: preservedOriginalFare,
        finalFare: calculatedFinalFare,
        encodedPolyline: preservedEncodedPolyline,
        arrivalPolyline: preservedArrivalPolyline,
        actualDistanceKm: newer.actualDistanceKm ?? actualDistanceKm,
        actualDurationMinutes:
            newer.actualDurationMinutes ?? actualDurationMinutes,
        actualEncodedPolyline: preservedActualEncodedPolyline,
        tripEndedAt: newer.tripEndedAt ?? tripEndedAt,
        terminationCategory: newer.terminationCategory ?? terminationCategory,
        safetyTerminationReason:
            newer.safetyTerminationReason ?? safetyTerminationReason,
        safetyTerminatedAt: newer.safetyTerminatedAt ?? safetyTerminatedAt,
        pickup: newer.pickup ?? pickup,
        destination: newer.destination ?? destination,
        vehicle: newer.vehicle ?? vehicle,
        payment: newer.payment ?? payment,
      );
    }

    // Case 2: Newer response has authoritative promotion and fare information.
    if (newHasPromo) {
      final double newerOriginal = newer.originalFare ?? newer.estimatedFare;
      final double newerDiscount = newer.discountAmount ?? 0;
      var newerFinal = newer.finalFare ?? newerOriginal - newerDiscount;

      if (newerFinal < 0) newerFinal = 0;

      return newer.copyWith(
        finalFare: newerFinal,
        encodedPolyline: preservedEncodedPolyline,
        arrivalPolyline: preservedArrivalPolyline,
        actualDistanceKm: newer.actualDistanceKm ?? actualDistanceKm,
        actualDurationMinutes:
            newer.actualDurationMinutes ?? actualDurationMinutes,
        actualEncodedPolyline: preservedActualEncodedPolyline,
        tripEndedAt: newer.tripEndedAt ?? tripEndedAt,
        terminationCategory: newer.terminationCategory ?? terminationCategory,
        safetyTerminationReason:
            newer.safetyTerminationReason ?? safetyTerminationReason,
        safetyTerminatedAt: newer.safetyTerminatedAt ?? safetyTerminatedAt,
        pickup: newer.pickup ?? pickup,
        destination: newer.destination ?? destination,
        vehicle: newer.vehicle ?? vehicle,
        payment: newer.payment ?? payment,
      );
    }

    // Default fallback - still preserve polylines and other critical fields
    return newer.copyWith(
      encodedPolyline: preservedEncodedPolyline,
      arrivalPolyline: preservedArrivalPolyline,
      actualDistanceKm: newer.actualDistanceKm ?? actualDistanceKm,
      actualDurationMinutes:
          newer.actualDurationMinutes ?? actualDurationMinutes,
      actualEncodedPolyline: preservedActualEncodedPolyline,
      tripEndedAt: newer.tripEndedAt ?? tripEndedAt,
      terminationCategory: newer.terminationCategory ?? terminationCategory,
      safetyTerminationReason:
          newer.safetyTerminationReason ?? safetyTerminationReason,
      safetyTerminatedAt: newer.safetyTerminatedAt ?? safetyTerminatedAt,
      pickup: newer.pickup ?? pickup,
      destination: newer.destination ?? destination,
      vehicle: newer.vehicle ?? vehicle,
      payment: newer.payment ?? payment,
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
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
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
        4 => 'WAITING_RETURN_CONFIRM',
        5 => 'RETURN_CONFIRMED',
        6 => 'WAITING_PAYMENT',
        7 => 'COMPLETED',
        8 => 'CANCELLED',
        _ => value.toString(),
      };
    }

    final text = value.toString();
    return switch (text) {
      '0' => 'ACCEPTED',
      '1' => 'DRIVER_ARRIVING',
      '2' => 'ARRIVED',
      '3' => 'IN_PROGRESS',
      '4' => 'WAITING_RETURN_CONFIRM',
      '5' => 'RETURN_CONFIRMED',
      '6' => 'WAITING_PAYMENT',
      '7' => 'COMPLETED',
      '8' => 'CANCELLED',
      _ => text,
    };
  }
}

class TripPaymentSummary {
  const TripPaymentSummary({
    required this.paymentStatus,
    required this.amount,
    required this.currency,
    required this.message,
    this.paymentId,
    this.paymentMethod,
    this.paidAt,
    this.successfulPaymentAmount = 0,
    this.remainingPayableAmount = 0,
    this.refundObligationAmount = 0,
    this.reconciliationStatus,
    this.refundStatus,
  });

  final int? paymentId;
  final String? paymentMethod;
  final String paymentStatus;
  final double amount;
  final String currency;
  final DateTime? paidAt;
  final double successfulPaymentAmount;
  final double remainingPayableAmount;
  final double refundObligationAmount;
  final String? reconciliationStatus;
  final String? refundStatus;
  final String message;

  bool get isSuccess => paymentStatus.toLowerCase() == 'success';
  bool get requiresPayment => remainingPayableAmount > 0;
  bool get isRefundPending =>
      reconciliationStatus?.toUpperCase() == 'REFUND_PENDING';
  bool get isRefunded => reconciliationStatus?.toUpperCase() == 'REFUNDED';

  factory TripPaymentSummary.fromJson(Map<String, dynamic> json) {
    return TripPaymentSummary(
      paymentId: (_value(json, ApiKeys.paymentId) as num?)?.toInt(),
      paymentMethod: _value(json, ApiKeys.paymentMethod)?.toString(),
      paymentStatus:
          _value(json, ApiKeys.paymentStatus)?.toString() ?? 'Pending',
      amount: (_value(json, ApiKeys.amount) as num?)?.toDouble() ?? 0,
      currency: _value(json, ApiKeys.currency)?.toString() ?? 'VND',
      paidAt: _value(json, ApiKeys.paidAt) == null
          ? null
          : DateTime.tryParse(_value(json, ApiKeys.paidAt).toString()),
      successfulPaymentAmount:
          (_value(json, ApiKeys.successfulPaymentAmount) as num?)?.toDouble() ?? 0,
      remainingPayableAmount:
          (_value(json, ApiKeys.remainingPayableAmount) as num?)?.toDouble() ?? 0,
      refundObligationAmount:
          (_value(json, ApiKeys.refundObligationAmount) as num?)?.toDouble() ?? 0,
      reconciliationStatus:
          _value(json, ApiKeys.reconciliationStatus)?.toString(),
      refundStatus: _value(json, ApiKeys.refundStatus)?.toString(),
      message:
          _value(json, ApiKeys.message)?.toString() ??
          LocaleProvider.currentLocalizations.payDriverToComplete,
    );
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
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
    this.driverLatitude,
    this.driverLongitude,
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
  final double? driverLatitude;
  final double? driverLongitude;
  final int? customerConfirmRemainingSeconds;

  factory BookingDriverOffer.fromJson(Map<String, dynamic> json) {
    return BookingDriverOffer(
      offerId: (_value(json, ApiKeys.offerId) as num?)?.toInt() ?? 0,
      driverId: _value(json, ApiKeys.driverId)?.toString() ?? '',
      driverName:
          _value(json, ApiKeys.driverName)?.toString() ?? 'Tai xe SafeRide',
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
      driverLatitude: (_value(json, ApiKeys.driverLatitude) as num?)
          ?.toDouble(),
      driverLongitude: (_value(json, ApiKeys.driverLongitude) as num?)
          ?.toDouble(),
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
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }
}

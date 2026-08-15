import '../../../../customer/booking/data/models/booking_catalog.dart';
import '../../../../customer/booking/data/models/booking_location.dart';
import '../../../../customer/booking/data/models/booking_response.dart';
import 'package:safe_ride/features/shared/feedback/data/models/driver_rating_item.dart';
import '../../../../../core/localization/locale_provider.dart';
import 'history_trip.dart';

class TripDetailsViewData {
  const TripDetailsViewData({
    required this.historyTrip,
    this.booking,
    this.feedback,
  });

  final HistoryTrip historyTrip;
  final BookingResponse? booking;
  final DriverRatingItem? feedback;

  int get bookingId => booking?.bookingId ?? historyTrip.id;
  int? get tripId => booking?.tripId ?? historyTrip.tripId;

  DateTime get bookingTime => booking?.scheduledAt ?? historyTrip.time;

  BookingLocation? get pickupLocation => booking?.pickup;
  BookingLocation? get destinationLocation => booking?.destination;

  String get pickupAddress =>
      _firstNonEmpty(booking?.pickup?.address, historyTrip.pickup) ?? '--';

  String get destinationAddress =>
      _firstNonEmpty(booking?.destination?.address, historyTrip.destination) ??
      '--';

  String? get routePolyline {
    final actualEncodedPolyline = booking?.actualEncodedPolyline;
    if (actualEncodedPolyline != null && actualEncodedPolyline.isNotEmpty) {
      return actualEncodedPolyline;
    }

    final encodedPolyline = booking?.encodedPolyline;
    if (encodedPolyline != null && encodedPolyline.isNotEmpty) {
      return encodedPolyline;
    }

    return null;
  }

  double get distanceKm {
    final actualDistanceKm = booking?.actualDistanceKm;
    if (actualDistanceKm != null && actualDistanceKm > 0) {
      return actualDistanceKm;
    }

    final estimatedDistanceKm = booking?.estimatedDistanceKm;
    if (estimatedDistanceKm != null && estimatedDistanceKm > 0) {
      return estimatedDistanceKm;
    }

    return historyTrip.distanceKm;
  }

  int? get durationMinutes {
    final actualDurationMinutes = booking?.actualDurationMinutes;
    if (actualDurationMinutes != null && actualDurationMinutes > 0) {
      return actualDurationMinutes;
    }

    final estimatedDurationMinutes = booking?.estimatedDurationMinutes;
    if (estimatedDurationMinutes != null && estimatedDurationMinutes > 0) {
      return estimatedDurationMinutes;
    }

    return null;
  }

  String? get driverName =>
      _firstNonEmpty(booking?.driverOffer?.driverName, historyTrip.driverName);

  String? get driverAvatarUrl => _firstNonEmpty(
    booking?.driverOffer?.driverAvatarUrl,
    historyTrip.driverAvatar,
  );

  double? get driverRating {
    final rating = booking?.driverOffer?.rating;
    if (rating != null && rating > 0) {
      return rating;
    }

    return historyTrip.driverRating;
  }

  int? get driverTripCount {
    final tripCount = booking?.driverOffer?.tripCount;
    if (tripCount != null && tripCount > 0) {
      return tripCount;
    }

    return null;
  }

  int? get driverExperienceYears {
    final experienceYears = booking?.driverOffer?.experienceYears;
    if (experienceYears != null && experienceYears > 0) {
      return experienceYears;
    }

    return null;
  }

  String? get driverLicenseClass =>
      _cleanText(booking?.driverOffer?.licenseClass);

  BookingVehicleOption? get vehicle => booking?.vehicle;

  String get vehicleName =>
      _firstNonEmpty(vehicle?.name, historyTrip.vehicleName) ?? 'SafeRide';

  String? get plateNumber => _cleanText(vehicle?.plateNumber);

  String? get vehicleColor => _cleanText(vehicle?.color);

  bool get isMotorbike => vehicle?.isMotorbike ?? historyTrip.isMotorbike;

  double get baseFare {
    final originalFare = booking?.originalFare;
    if (originalFare != null && originalFare > 0) {
      return originalFare;
    }

    final estimatedFare = booking?.estimatedFare;
    if (estimatedFare != null && estimatedFare > 0) {
      return estimatedFare;
    }

    return historyTrip.fare;
  }

  double get discountAmount {
    final discount = booking?.discountAmount;
    if (discount != null && discount > 0) {
      return discount;
    }

    return 0;
  }

  double get totalFare {
    final paymentAmount = payment?.amount;
    if (paymentAmount != null && paymentAmount > 0) {
      return paymentAmount;
    }

    final finalFare = booking?.finalFare;
    if (finalFare != null && finalFare > 0) {
      return finalFare;
    }

    final calculatedFare = baseFare - discountAmount;
    if (calculatedFare > 0) {
      return calculatedFare;
    }

    return historyTrip.fare;
  }

  TripPaymentSummary? get payment => booking?.payment;

  String? get paymentMethod => _cleanText(payment?.paymentMethod);

  String? get paymentStatus => _cleanText(payment?.paymentStatus);

  String? get paymentMessage => _cleanText(payment?.message);

  DateTime? get paidAt => payment?.paidAt;

  String get normalizedStatus {
    final tripStatus = _cleanText(booking?.tripStatus);
    if (tripStatus != null) {
      return tripStatus.toUpperCase();
    }

    final bookingStatus = _cleanText(booking?.bookingStatus);
    if (bookingStatus != null) {
      return bookingStatus.toUpperCase();
    }

    return switch (historyTrip.status) {
      HistoryTripStatus.completed => 'COMPLETED',
      HistoryTripStatus.cancelled => 'CANCELLED',
      HistoryTripStatus.booked => 'BOOKED',
    };
  }

  String get statusLabel {
    final l10n = LocaleProvider.currentLocalizations;
    return switch (normalizedStatus) {
      'COMPLETED' || '5' => l10n.statusCompleted,
      'CANCELLED' || 'CANCEL' || '3' || '8' => l10n.statusCancelled,
      'EXPIRED' || '4' => l10n.expired,
      'WAITING_PAYMENT' || '6' => l10n.waitingDriverPayment,
      'RETURN_CONFIRMED' => l10n.returnConfirmedStatus,
      'WAITING_RETURN_CONFIRM' => l10n.waitingReturnConfirmation,
      'IN_PROGRESS' => l10n.statusInProgress,
      'ARRIVED' => l10n.statusArrived,
      'DRIVER_ARRIVING' => l10n.statusDriverArriving,
      'ACCEPTED' => l10n.statusAccepted,
      'DRIVERASSIGNED' || 'DRIVER_ASSIGNED' || '2' => l10n.driverConfirmed,
      'SEARCHING' || '1' => l10n.searchingDriver,
      'PENDINGSCHEDULE' ||
      'PENDING_SCHEDULE' ||
      '0' => l10n.awaitingConfirmation,
      'BOOKED' => l10n.historyFilterBooked,
      _ => l10n.processing,
    };
  }

  String get paymentStatusLabel {
    final l10n = LocaleProvider.currentLocalizations;
    final status = paymentStatus?.toUpperCase();
    return switch (status) {
      'SUCCESS' => l10n.paid,
      'PENDING' => l10n.waitingDriverPayment,
      'FAILED' => l10n.genericError,
      'CANCELLED' => l10n.statusCancelled,
      _ => payment == null ? l10n.unknown : l10n.processing,
    };
  }

  bool get isCancelled =>
      normalizedStatus == 'CANCELLED' ||
      normalizedStatus == 'CANCEL' ||
      normalizedStatus == 'EXPIRED' ||
      normalizedStatus == '3' ||
      normalizedStatus == '4' ||
      normalizedStatus == '8';

  bool get hasDriverInfo => driverName != null;

  bool get hasPaymentInfo => payment != null || totalFare > 0;

  bool get hasMapCoordinates =>
      _hasCoordinates(pickupLocation) || _hasCoordinates(destinationLocation);

  bool get hasFeedback => feedback != null;

  String get feedbackText =>
      feedback?.comment ??
      LocaleProvider.currentLocalizations.customerHasNotReviewed;

  int? get ratingScore => feedback?.score;

  String? get feedbackComment => feedback?.comment;

  String? get feedbackCustomerName => feedback?.customerName;

  String? get feedbackCustomerAvatarUrl => feedback?.customerAvatarUrl;

  DateTime? get feedbackCreatedAt => feedback?.createdAt;

  static bool _hasCoordinates(BookingLocation? location) {
    if (location == null) {
      return false;
    }

    return location.latitude != 0 || location.longitude != 0;
  }

  static String? _firstNonEmpty(String? primary, String? fallback) {
    return _cleanText(primary) ?? _cleanText(fallback);
  }

  static String? _cleanText(String? value) {
    if (value == null) {
      return null;
    }

    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }
}

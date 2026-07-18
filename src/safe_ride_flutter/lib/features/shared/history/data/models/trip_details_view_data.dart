import '../../../../customer/booking/data/models/booking_catalog.dart';
import '../../../../customer/booking/data/models/booking_location.dart';
import '../../../../customer/booking/data/models/booking_response.dart';
import 'history_trip.dart';

class TripDetailsViewData {
  const TripDetailsViewData({required this.historyTrip, this.booking});

  final HistoryTrip historyTrip;
  final BookingResponse? booking;

  int get bookingId => booking?.bookingId ?? historyTrip.id;
  int? get tripId => booking?.tripId;

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
    return switch (normalizedStatus) {
      'COMPLETED' || '5' => 'Hoàn thành',
      'CANCELLED' || 'CANCEL' || '3' || '8' => 'Đã hủy',
      'EXPIRED' || '4' => 'Hết hạn',
      'WAITING_PAYMENT' || '6' => 'Chờ thanh toán',
      'RETURN_CONFIRMED' => 'Đã xác nhận trả xe',
      'WAITING_RETURN_CONFIRM' => 'Chờ xác nhận trả xe',
      'IN_PROGRESS' => 'Đang di chuyển',
      'ARRIVED' => 'Đã đến điểm đón',
      'DRIVER_ARRIVING' => 'Tài xế đang đến',
      'ACCEPTED' => 'Đã nhận chuyến',
      'DRIVERASSIGNED' || 'DRIVER_ASSIGNED' || '2' => 'Đã ghép tài xế',
      'SEARCHING' || '1' => 'Đang tìm tài xế',
      'PENDINGSCHEDULE' || 'PENDING_SCHEDULE' || '0' => 'Chờ khởi hành',
      'BOOKED' => 'Đã đặt',
      _ => 'Đang xử lý',
    };
  }

  String get paymentStatusLabel {
    final status = paymentStatus?.toUpperCase();
    return switch (status) {
      'SUCCESS' => 'Đã thanh toán',
      'PENDING' => 'Chờ thanh toán',
      'FAILED' => 'Thanh toán thất bại',
      'CANCELLED' => 'Đã hủy thanh toán',
      _ => payment == null ? 'Chưa có thông tin' : 'Đang xử lý',
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

  bool get hasFeedback => false;

  String get feedbackText => 'Chưa có dữ liệu đánh giá cho chuyến đi này.';

  int? get ratingScore => null;

  String? get feedbackComment => null;

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

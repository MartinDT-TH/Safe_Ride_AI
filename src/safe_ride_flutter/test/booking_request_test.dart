import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/customer/booking/data/models/booking_location.dart';
import 'package:safe_ride/features/customer/booking/data/models/booking_response.dart';
import 'package:safe_ride/features/customer/booking/data/models/create_booking_request.dart';

void main() {
  test('scheduled booking request serializes backend contract', () {
    final scheduledAt = DateTime(2026, 6, 16, 8, 30);
    final request = CreateBookingRequest(
      vehicleId: 12,
      serviceTypeId: 3,
      bookingType: BookingType.scheduled,
      scheduledAt: scheduledAt,
      pickup: const BookingLocation(
        address: 'Điểm đón',
        latitude: 10.762622,
        longitude: 106.660172,
      ),
      destination: const BookingLocation(
        address: 'Điểm đến',
        latitude: 10.818797,
        longitude: 106.651856,
      ),
    );

    final json = request.toJson();

    expect(json['bookingType'], 'Scheduled');
    expect(json['vehicleId'], 12);
    expect(json['serviceTypeId'], 3);
    expect(json['scheduledAt'], scheduledAt.toUtc().toIso8601String());
    expect(json['pickupLatitude'], 10.762622);
    expect(json['destinationLongitude'], 106.651856);
  });

  test('scheduled booking response converts UTC to phone local time', () {
    final response = BookingResponse.fromJson({
      'bookingId': 42,
      'bookingType': 'Scheduled',
      'bookingStatus': 'PendingSchedule',
      'scheduledAt': '2026-06-16T01:30:00Z',
      'estimatedDistanceKm': 5.2,
      'estimatedDurationMinutes': 30,
      'estimatedFare': 72000,
      'encodedPolyline': '',
      'message': 'OK',
    });

    expect(response.scheduledAt, DateTime.utc(2026, 6, 16, 1, 30).toLocal());
    expect(response.scheduledAt!.isUtc, isFalse);
  });

  test('scheduled booking response treats legacy offset-less value as UTC', () {
    final response = BookingResponse.fromJson({
      'bookingId': 43,
      'bookingType': 'Scheduled',
      'bookingStatus': 'PendingSchedule',
      'scheduledAt': '2026-06-16T01:30:00',
      'estimatedDistanceKm': 5.2,
      'estimatedDurationMinutes': 30,
      'estimatedFare': 72000,
      'encodedPolyline': '',
      'message': 'OK',
    });

    expect(response.scheduledAt, DateTime.utc(2026, 6, 16, 1, 30).toLocal());
    expect(response.scheduledAt!.isUtc, isFalse);
  });

  test(
    'safety-terminated booking keeps partial fare and termination metadata',
    () {
      final response = BookingResponse.fromJson({
        'bookingId': 44,
        'bookingType': 'Now',
        'bookingStatus': 'Cancelled',
        'estimatedDistanceKm': 8.5,
        'estimatedDurationMinutes': 45,
        'estimatedFare': 120000,
        'originalFare': 72000,
        'discountAmount': 0,
        'finalFare': 72000,
        'encodedPolyline': '',
        'tripId': 99,
        'tripStatus': 'CANCELLED',
        'terminationCategory': 'SAFETY',
        'safetyTerminationReason': 'Phanh không đảm bảo an toàn',
        'safetyTerminatedAt': '2026-06-16T01:30:00Z',
        'payment': {
          'paymentStatus': 'Pending',
          'amount': 72000,
          'successfulPaymentAmount': 20000,
          'remainingPayableAmount': 52000,
          'refundObligationAmount': 0,
          'reconciliationStatus': 'PAYMENT_PENDING',
          'currency': 'VND',
          'message': 'Vui lòng thanh toán.',
        },
        'message': 'OK',
      });

      expect(response.finalFare, 72000);
      expect(response.discountAmount, 0);
      expect(response.terminationCategory, 'SAFETY');
      expect(response.safetyTerminationReason, 'Phanh không đảm bảo an toàn');
      expect(response.safetyTerminatedAt?.hour, 8);
      expect(response.safetyTerminatedAt?.minute, 30);
      expect(response.payment?.amount, 72000);
      expect(response.payment?.successfulPaymentAmount, 20000);
      expect(response.payment?.remainingPayableAmount, 52000);
      expect(response.payment?.requiresPayment, isTrue);
    },
  );

  test('safety termination merge does not restore released promotion', () {
    final active = BookingResponse.fromJson({
      'bookingId': 45,
      'bookingType': 'Now',
      'bookingStatus': 'DriverAssigned',
      'estimatedDistanceKm': 8.5,
      'estimatedDurationMinutes': 45,
      'estimatedFare': 120000,
      'originalFare': 120000,
      'promotionCode': 'SAFE10',
      'discountAmount': 10000,
      'finalFare': 110000,
      'encodedPolyline': '',
      'tripId': 100,
      'tripStatus': 'IN_PROGRESS',
      'message': 'OK',
    });
    final cancelled = BookingResponse.fromJson({
      'bookingId': 45,
      'bookingType': 'Now',
      'bookingStatus': 'Cancelled',
      'estimatedDistanceKm': 8.5,
      'estimatedDurationMinutes': 45,
      'estimatedFare': 120000,
      'originalFare': 72000,
      'discountAmount': 0,
      'finalFare': 72000,
      'encodedPolyline': '',
      'tripId': 100,
      'tripStatus': 'CANCELLED',
      'terminationCategory': 'SAFETY',
      'message': 'OK',
    });

    final merged = active.mergeWithPreservedPromotion(cancelled);

    expect(merged.promotionCode, isNull);
    expect(merged.discountAmount, 0);
    expect(merged.originalFare, 72000);
    expect(merged.finalFare, 72000);
  });

  test('safety refund pending does not navigate as remaining payable', () {
    final response = BookingResponse.fromJson({
      'bookingId': 46,
      'bookingType': 'Now',
      'bookingStatus': 'Cancelled',
      'estimatedDistanceKm': 0,
      'estimatedDurationMinutes': 0,
      'estimatedFare': 120000,
      'originalFare': 0,
      'discountAmount': 0,
      'finalFare': 0,
      'encodedPolyline': '',
      'tripId': 101,
      'tripStatus': 'CANCELLED',
      'terminationCategory': 'SAFETY',
      'payment': {
        'paymentStatus': 'Success',
        'amount': 120000,
        'successfulPaymentAmount': 120000,
        'remainingPayableAmount': 0,
        'refundObligationAmount': 120000,
        'reconciliationStatus': 'REFUND_PENDING',
        'refundStatus': 'REFUND_PENDING',
        'currency': 'VND',
        'message': 'Đang chờ hoàn tiền.',
      },
      'message': 'OK',
    });

    expect(response.payment?.requiresPayment, isFalse);
    expect(response.payment?.isRefundPending, isTrue);
    expect(response.payment?.refundObligationAmount, 120000);
  });

  test('completed zero-distance trip preserves explicit zero fare', () {
    final response = BookingResponse.fromJson({
      'bookingId': 47,
      'bookingType': 'Now',
      'bookingStatus': 'Completed',
      'estimatedDistanceKm': 5.2,
      'estimatedDurationMinutes': 30,
      'estimatedFare': 72000,
      'originalFare': 0,
      'discountAmount': 10000,
      'finalFare': 0,
      'actualDistanceKm': 0,
      'encodedPolyline': '',
      'message': 'OK',
    });

    expect(response.originalFare, 0);
    expect(response.finalFare, 0);
  });

  test('promotion merge preserves authoritative zero fare', () {
    final current = BookingResponse.fromJson({
      'bookingId': 48,
      'bookingType': 'Now',
      'bookingStatus': 'DriverAssigned',
      'estimatedDistanceKm': 5.2,
      'estimatedDurationMinutes': 30,
      'estimatedFare': 72000,
      'originalFare': 72000,
      'promotionCode': 'SAFE10',
      'discountAmount': 10000,
      'finalFare': 62000,
      'encodedPolyline': '',
      'message': 'OK',
    });
    final newer = BookingResponse.fromJson({
      'bookingId': 48,
      'bookingType': 'Now',
      'bookingStatus': 'Completed',
      'estimatedDistanceKm': 5.2,
      'estimatedDurationMinutes': 30,
      'estimatedFare': 72000,
      'originalFare': 0,
      'discountAmount': 0,
      'finalFare': 0,
      'actualDistanceKm': 0,
      'encodedPolyline': '',
      'message': 'OK',
    });

    final merged = current.mergeWithPreservedPromotion(newer);

    expect(merged.originalFare, 0);
    expect(merged.finalFare, 0);
  });
}

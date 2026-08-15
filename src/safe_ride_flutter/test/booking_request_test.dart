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
}

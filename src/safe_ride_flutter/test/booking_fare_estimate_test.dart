import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/customer/booking/data/models/booking_fare_estimate.dart';

void main() {
  test('parses a positive hourly fare estimate', () {
    final estimate = BookingFareEstimate.fromJson({
      'estimatedDistanceKm': 0,
      'estimatedDurationMinutes': 120,
      'encodedPolyline': '',
      'estimatedFare': 140000,
    });

    expect(estimate.estimatedFare, 140000);
    expect(estimate.estimatedDurationMinutes, 120);
  });

  test('rejects a zero fare instead of displaying zero dong', () {
    expect(
      () => BookingFareEstimate.fromJson({
        'estimatedDistanceKm': 0,
        'estimatedDurationMinutes': 120,
        'encodedPolyline': '',
        'estimatedFare': 0,
      }),
      throwsFormatException,
    );
  });

  test('rejects a response without estimated fare', () {
    expect(
      () => BookingFareEstimate.fromJson({
        'estimatedDistanceKm': 0,
        'estimatedDurationMinutes': 120,
        'encodedPolyline': '',
      }),
      throwsFormatException,
    );
  });
}

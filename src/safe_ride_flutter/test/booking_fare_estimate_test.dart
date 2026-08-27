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

  test('maps the accepted pricing breakdown', () {
    final estimate = BookingFareEstimate.fromJson({
      'estimatedDistanceKm': 20,
      'estimatedDurationMinutes': 60,
      'encodedPolyline': 'route',
      'estimatedFare': 279000,
      'normalFare': 220000,
      'surgedFare': 264000,
      'surgeAmount': 44000,
      'longDistanceComponent': 15000,
      'minimumServiceFare': 30000,
      'surgeMultiplier': 1.2,
    });

    expect(estimate.normalFare, 220000);
    expect(estimate.surgedFare, 264000);
    expect(estimate.surgeAmount, 44000);
    expect(estimate.longDistanceComponent, 15000);
    expect(estimate.minimumServiceFare, 30000);
    expect(estimate.surgeMultiplier, 1.2);
  });
}

import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/trip_sharing/data/models/trip_share_models.dart';

void main() {
  test('parses the minimal shared trip tracking contract', () {
    final tracking = SharedTripTracking.fromJson({
      'tripShareId': 42,
      'tripStatus': 'IN_PROGRESS',
      'pickup': {'latitude': 10.76, 'longitude': 106.66, 'address': 'Điểm đón'},
      'destination': {'latitude': 10.8, 'longitude': 106.7},
      'currentDriverLocation': {'latitude': 10.77, 'longitude': 106.67},
      'lastLocationUpdate': '2026-07-15T09:00:00Z',
      'routePolyline': 'encoded',
      'driver': {'fullName': 'Tài xế A', 'avatarUrl': null, 'rating': 4.8},
      'vehicle': {
        'brandModel': 'Toyota Vios',
        'color': 'Trắng',
        'maskedPlateNumber': '43A***45',
      },
    });

    expect(tracking.tripShareId, 42);
    expect(tracking.driverName, 'Tài xế A');
    expect(tracking.maskedPlateNumber, '43A***45');
    expect(tracking.currentDriverLocation?.latitude, 10.77);
  });

  test('copyWith updates realtime fields without exposing edit actions', () {
    final tracking = SharedTripTracking.fromJson({
      'tripShareId': 1,
      'tripStatus': 'ARRIVED',
      'pickup': {'latitude': 1.0, 'longitude': 2.0},
      'driver': {'fullName': 'Driver'},
      'vehicle': {'brandModel': 'Car', 'maskedPlateNumber': '***'},
    });

    final updated = tracking.copyWith(
      tripStatus: 'IN_PROGRESS',
      currentDriverLocation: const SharedTripPoint(latitude: 3, longitude: 4),
    );

    expect(updated.tripStatus, 'IN_PROGRESS');
    expect(updated.currentDriverLocation?.longitude, 4);
    expect(updated.pickup.latitude, tracking.pickup.latitude);
  });
}

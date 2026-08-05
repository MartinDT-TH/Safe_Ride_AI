import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/shared/history/data/models/history_trip.dart';

void main() {
  test('maps completed trip status and prefers final fare', () {
    final trip = HistoryTrip.fromJson({
      'id': 101,
      'pickupAddress': 'Bến xe Miền Đông',
      'destinationAddress': 'Sân bay Tân Sơn Nhất',
      'occurredAt': '2026-06-26T10:30:00Z',
      'estimatedDistanceKm': 12.5,
      'estimatedFare': 150000,
      'finalFare': 120000,
      'bookingStatus': 'Completed',
      'vehicleName': 'Toyota Vios',
      'isMotorbike': false,
    });

    expect(trip.id, 101);
    expect(trip.status, HistoryTripStatus.completed);
    expect(trip.fare, 120000);
    expect(trip.distanceKm, 12.5);
  });

  test('maps cancelled and expired statuses to cancelled history bucket', () {
    final cancelled = HistoryTrip.fromJson({
      'id': 202,
      'pickupAddress': 'Quận 1',
      'destinationAddress': 'Quận 7',
      'updatedAt': '2026-06-26T11:00:00Z',
      'estimatedFare': 80000,
      'bookingStatus': 'Cancelled',
    });

    final expired = HistoryTrip.fromJson({
      'id': 203,
      'pickupAddress': 'Quận 3',
      'destinationAddress': 'Quận 5',
      'updatedAt': '2026-06-26T11:15:00Z',
      'estimatedFare': 90000,
      'bookingStatus': 'Expired',
    });

    expect(cancelled.status, HistoryTripStatus.cancelled);
    expect(expired.status, HistoryTripStatus.cancelled);
  });

  test('keeps pending or assigned bookings in booked bucket', () {
    final pending = HistoryTrip.fromJson({
      'id': 301,
      'pickupAddress': 'Thủ Đức',
      'destinationAddress': 'Bình Thạnh',
      'scheduledAt': '2026-06-27T02:00:00Z',
      'estimatedFare': 100000,
      'bookingStatus': 'PendingSchedule',
    });

    final assigned = HistoryTrip.fromJson({
      'id': 302,
      'pickupAddress': 'Nhà Bè',
      'destinationAddress': 'Quận 4',
      'updatedAt': '2026-06-26T12:00:00Z',
      'estimatedFare': 110000,
      'bookingStatus': 'DriverAssigned',
    });

    expect(pending.status, HistoryTripStatus.booked);
    expect(assigned.status, HistoryTripStatus.booked);
  });
}

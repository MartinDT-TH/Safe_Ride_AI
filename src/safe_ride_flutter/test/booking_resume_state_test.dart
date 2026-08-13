import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/customer/booking/data/models/booking_response.dart';

void main() {
  group('BookingResponse resume state', () {
    test('driver accepted offer stays in searching flow', () {
      final booking = _booking(
        bookingStatus: 'Searching',
        tripId: null,
        offerStatus: 'DriverAccepted',
      );

      expect(booking.isSearchingNowBooking, isTrue);
      expect(booking.isTrackableTrip, isFalse);
    });

    test('assigned booking with trip opens tracking flow', () {
      final booking = _booking(
        bookingStatus: 'DriverAssigned',
        tripId: 42,
        offerStatus: 'CustomerConfirmed',
      );

      expect(booking.isSearchingNowBooking, isFalse);
      expect(booking.isTrackableTrip, isTrue);
    });

    test('assigned status without trip does not open tracking flow', () {
      final booking = _booking(
        bookingStatus: 'DriverAssigned',
        tripId: null,
        offerStatus: 'CustomerConfirmed',
      );

      expect(booking.isTrackableTrip, isFalse);
    });
  });
}

BookingResponse _booking({
  required String bookingStatus,
  required int? tripId,
  required String offerStatus,
}) {
  return BookingResponse(
    bookingId: 1,
    bookingType: 'Now',
    bookingStatus: bookingStatus,
    estimatedDistanceKm: 1,
    estimatedDurationMinutes: 5,
    estimatedFare: 100000,
    encodedPolyline: '',
    message: '',
    tripId: tripId,
    driverOffer: BookingDriverOffer(
      offerId: 2,
      driverId: 'driver-1',
      driverName: 'Driver',
      rating: 5,
      tripCount: 10,
      experienceYears: 2,
      licenseClass: 'B2',
      expiresAt: DateTime.utc(2026, 8, 13),
      offerStatus: offerStatus,
    ),
  );
}

import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/features/customer/booking/data/models/booking_response.dart';
import 'package:safe_ride/features/shared/feedback/data/models/driver_rating_item.dart';
import 'package:safe_ride/features/shared/feedback/data/models/driver_rating_summary.dart';
import 'package:safe_ride/features/shared/feedback/domain/repositories/feedback_repository.dart';
import 'package:safe_ride/features/shared/history/data/models/history_trip.dart';
import 'package:safe_ride/features/shared/history/domain/repositories/trip_details_repository.dart';
import 'package:safe_ride/features/shared/history/presentation/providers/trip_details_provider.dart';

void main() {
  test('loads the customer comment using the driver from booking details', () async {
    const driverId = '11111111-1111-1111-1111-111111111111';
    const tripId = 77;
    final detailsRepository = _FakeTripDetailsRepository(
      BookingResponse.fromJson({
        'bookingId': 10,
        'bookingType': 'Now',
        'bookingStatus': 'Completed',
        'estimatedDistanceKm': 2.5,
        'estimatedDurationMinutes': 10,
        'estimatedFare': 62000,
        'encodedPolyline': '',
        'message': 'OK',
        'tripId': tripId,
        'tripStatus': 'COMPLETED',
        'driverOffer': {
          'offerId': 20,
          'driverId': driverId,
          'driverName': 'Tai xe SafeRide',
          'rating': 5,
          'tripCount': 1,
          'experienceYears': 1,
          'licenseClass': 'B2',
        },
      }),
    );
    final feedbackRepository = _FakeFeedbackRepository(
      DriverRatingSummary(
        driverId: driverId,
        averageRating: 5,
        totalRatings: 1,
        ratings: [
          DriverRatingItem(
            id: 1,
            tripId: tripId,
            customerName: 'Khach hang',
            score: 5,
            comment: 'Tai xe rat than thien',
            createdAt: DateTime.utc(2026, 8, 13),
          ),
        ],
      ),
    );
    final historyTrip = HistoryTrip.fromJson({
      'id': 10,
      'tripId': tripId,
      'pickupAddress': 'Diem don',
      'destinationAddress': 'Diem den',
      'occurredAt': '2026-08-13T10:00:00Z',
      'estimatedDistanceKm': 2.5,
      'estimatedFare': 62000,
      'finalFare': 62000,
      'bookingStatus': 'Completed',
      'vehicleName': 'SafeRide',
    });
    final provider = TripDetailsProvider.create(
      detailsRepository,
      historyTrip,
      feedbackRepository: feedbackRepository,
    );

    await provider.loadDetails('access-token');

    expect(feedbackRepository.requestedDriverId, driverId);
    expect(provider.tripDetails.ratingScore, 5);
    expect(provider.tripDetails.feedbackComment, 'Tai xe rat than thien');
  });
}

class _FakeTripDetailsRepository implements TripDetailsRepository {
  _FakeTripDetailsRepository(this.response);

  final BookingResponse response;

  @override
  Future<BookingResponse> getTripDetails(
    String accessToken, {
    required int bookingId,
  }) async {
    return response;
  }
}

class _FakeFeedbackRepository implements FeedbackRepository {
  _FakeFeedbackRepository(this.response);

  final DriverRatingSummary response;
  String? requestedDriverId;

  @override
  Future<DriverRatingSummary> getDriverRatings(
    String accessToken, {
    required String driverId,
  }) async {
    requestedDriverId = driverId;
    return response;
  }
}

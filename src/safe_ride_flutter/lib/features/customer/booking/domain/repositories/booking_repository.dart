import '../../data/models/booking_catalog.dart';
import '../../data/models/promo_model.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';
import '../../data/models/nearby_driver.dart';

abstract class BookingRepository {
  Future<List<PromoModel>> getAvailablePromotions(String accessToken);

  Future<BookingCatalog> getCatalog(String accessToken);

  Future<BookingFareEstimate> estimateFare(
    String accessToken, {
    required int vehicleId,
    required int serviceTypeId,
    required BookingLocation pickup,
    BookingLocation? destination,
    int? estimatedHours,
  });

  Future<BookingResponse> createBooking(
    String accessToken,
    CreateBookingRequest request,
  );

  Future<BookingResponse> getBookingDetails(
    String accessToken, {
    required int bookingId,
  });

  Future<BookingResponse?> getActiveBooking(String accessToken);

  Future<BookingResponse> cancelBooking(
    String accessToken, {
    required int bookingId,
    required String reason,
  });

  Future<BookingResponse> confirmDriver(
    String accessToken, {
    required int bookingId,
  });

  Future<BookingResponse> confirmDriverOffer(
    String accessToken, {
    required int bookingId,
    required int offerId,
  });

  Future<void> completeTrip(String accessToken, {required int tripId});

  Future<void> submitTripRating(
    String accessToken, {
    required int tripId,
    required int ratingScore,
    String? comment,
  });

  Future<void> submitTripReport(
    String accessToken, {
    required int bookingId,
    required String subject,
    required String description,
  });

  Future<BookingResponse> rejectDriver(
    String accessToken, {
    required int bookingId,
  });

  Future<List<NearbyDriver>> getNearbyDrivers(
    String accessToken, {
    required double latitude,
    required double longitude,
  });
}

import '../../domain/repositories/booking_repository.dart';
import '../models/nearby_driver.dart';
import '../models/promo_model.dart';
import '../datasources/booking_catalog_datasource.dart';
import '../datasources/booking_remote_datasource.dart';
import '../models/booking_catalog.dart';
import '../models/booking_fare_estimate.dart';
import '../models/booking_location.dart';
import '../models/booking_response.dart';
import '../models/create_booking_request.dart';

class BookingRepositoryImpl implements BookingRepository {
  BookingRepositoryImpl(this._remoteDatasource, this._catalogDatasource);

  final BookingRemoteDatasource _remoteDatasource;
  final BookingCatalogDatasource _catalogDatasource;

  @override
  Future<List<PromoModel>> getAvailablePromotions(String accessToken) {
    return _remoteDatasource.getAvailablePromotions(accessToken);
  }

  @override
  Future<BookingCatalog> getCatalog(String accessToken) {
    return _catalogDatasource.getCatalog(accessToken);
  }

  @override
  Future<BookingFareEstimate> estimateFare(
    String accessToken, {
    required int vehicleId,
    required int serviceTypeId,
    required BookingLocation pickup,
    BookingLocation? destination,
    int? estimatedHours,
  }) {
    return _remoteDatasource.estimateFare(
      accessToken,
      vehicleId: vehicleId,
      serviceTypeId: serviceTypeId,
      pickup: pickup,
      destination: destination,
      estimatedHours: estimatedHours,
    );
  }

  @override
  Future<BookingResponse> createBooking(
    String accessToken,
    CreateBookingRequest request,
  ) {
    return _remoteDatasource.createBooking(accessToken, request);
  }

  @override
  Future<BookingResponse> getBookingDetails(
    String accessToken, {
    required int bookingId,
  }) {
    return _remoteDatasource.getBookingDetails(
      accessToken,
      bookingId: bookingId,
    );
  }

  @override
  Future<BookingResponse?> getActiveBooking(String accessToken) {
    return _remoteDatasource.getActiveBooking(accessToken);
  }

  @override
  Future<BookingResponse> cancelBooking(
    String accessToken, {
    required int bookingId,
    required String reason,
  }) {
    return _remoteDatasource.cancelBooking(
      accessToken,
      bookingId: bookingId,
      reason: reason,
    );
  }

  @override
  Future<BookingResponse> confirmDriver(
    String accessToken, {
    required int bookingId,
  }) {
    return _remoteDatasource.confirmDriver(accessToken, bookingId: bookingId);
  }

  @override
  Future<BookingResponse> confirmDriverOffer(
    String accessToken, {
    required int bookingId,
    required int offerId,
  }) {
    return _remoteDatasource.confirmDriverOffer(
      accessToken,
      bookingId: bookingId,
      offerId: offerId,
    );
  }

  @override
  Future<void> completeTrip(String accessToken, {required int tripId}) {
    return _remoteDatasource.completeTrip(accessToken, tripId: tripId);
  }

  @override
  Future<void> submitTripRating(
    String accessToken, {
    required int tripId,
    required int ratingScore,
    String? comment,
  }) {
    return _remoteDatasource.submitTripRating(
      accessToken,
      tripId: tripId,
      ratingScore: ratingScore,
      comment: comment,
    );
  }

  @override
  Future<BookingResponse> rejectDriver(
    String accessToken, {
    required int bookingId,
  }) {
    return _remoteDatasource.rejectDriver(accessToken, bookingId: bookingId);
  }

  @override
  Future<List<NearbyDriver>> getNearbyDrivers(
    String accessToken, {
    required double latitude,
    required double longitude,
  }) {
    return _remoteDatasource.getNearbyDrivers(
      accessToken,
      latitude: latitude,
      longitude: longitude,
    );
  }
}

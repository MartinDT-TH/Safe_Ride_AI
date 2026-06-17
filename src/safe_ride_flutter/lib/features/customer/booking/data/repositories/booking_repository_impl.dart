import '../../domain/repositories/booking_repository.dart';
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
}

import '../../domain/repositories/booking_repository.dart';
import '../datasources/booking_catalog_datasource.dart';
import '../datasources/booking_remote_datasource.dart';
import '../models/booking_catalog.dart';
import '../models/booking_response.dart';
import '../models/create_booking_request.dart';

class BookingRepositoryImpl implements BookingRepository {
  BookingRepositoryImpl(this._remoteDatasource, this._catalogDatasource);

  final BookingRemoteDatasource _remoteDatasource;
  final BookingCatalogDatasource _catalogDatasource;

  @override
  Future<BookingCatalog> getCatalog() {
    return _catalogDatasource.getCatalog();
  }

  @override
  Future<BookingResponse> createBooking(
    String accessToken,
    CreateBookingRequest request,
  ) {
    return _remoteDatasource.createBooking(accessToken, request);
  }
}

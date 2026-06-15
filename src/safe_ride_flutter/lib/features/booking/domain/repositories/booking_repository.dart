import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';

abstract class BookingRepository {
  Future<BookingCatalog> getCatalog();

  Future<BookingResponse> createBooking(
    String accessToken,
    CreateBookingRequest request,
  );
}

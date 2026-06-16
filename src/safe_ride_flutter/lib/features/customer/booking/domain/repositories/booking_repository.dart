import '../../data/models/booking_catalog.dart';
import '../../data/models/booking_fare_estimate.dart';
import '../../data/models/booking_location.dart';
import '../../data/models/booking_response.dart';
import '../../data/models/create_booking_request.dart';

abstract class BookingRepository {
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
}


import '../../../../customer/booking/data/models/booking_response.dart';

abstract class TripDetailsRepository {
  Future<BookingResponse> getTripDetails(
    String accessToken, {
    required int bookingId,
  });
}

class TripDetailsRepositoryException implements Exception {
  const TripDetailsRepositoryException(this.message);

  final String message;

  @override
  String toString() => message;
}

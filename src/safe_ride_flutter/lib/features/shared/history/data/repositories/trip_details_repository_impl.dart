import '../../../../../core/constants/app_strings.dart';
import '../../../../customer/booking/data/datasources/booking_remote_datasource.dart';
import '../../../../customer/booking/data/models/booking_response.dart';
import '../../../../customer/booking/domain/repositories/booking_repository.dart';
import '../../domain/repositories/trip_details_repository.dart';

class TripDetailsRepositoryImpl implements TripDetailsRepository {
  TripDetailsRepositoryImpl(this._bookingRepository);

  final BookingRepository _bookingRepository;

  @override
  Future<BookingResponse> getTripDetails(
    String accessToken, {
    required int bookingId,
  }) async {
    try {
      return await _bookingRepository.getBookingDetails(
        accessToken,
        bookingId: bookingId,
      );
    } on BookingApiException catch (exception) {
      throw TripDetailsRepositoryException(exception.message);
    } catch (_) {
      throw const TripDetailsRepositoryException(AppStrings.genericError);
    }
  }
}

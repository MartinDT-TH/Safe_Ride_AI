import 'package:dio/dio.dart';

import '../../../../core/constants/app_strings.dart';
import '../../../../core/network/dio_client.dart';
import '../models/booking_response.dart';
import '../models/create_booking_request.dart';

class BookingRemoteDatasource {
  BookingRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<BookingResponse> createBooking(
    String accessToken,
    CreateBookingRequest request,
  ) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.bookings,
        data: request.toJson(),
        options: Options(
          headers: {ApiKeys.authorization: '${ApiKeys.bearer} $accessToken'},
        ),
      );

      return BookingResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map && data[ApiKeys.detail] != null) {
        throw BookingApiException(data[ApiKeys.detail].toString());
      }
      throw const BookingApiException(BookingStrings.bookingFailed);
    }
  }
}

class BookingApiException implements Exception {
  const BookingApiException(this.message);

  final String message;

  @override
  String toString() => message;
}

import 'package:dio/dio.dart';

import '../../../../core/constants/app_strings.dart';
import '../../../../core/network/auth_header.dart';
import '../../../../core/network/dio_client.dart';
import '../models/booking_response.dart';
import '../models/booking_fare_estimate.dart';
import '../models/booking_location.dart';
import '../models/create_booking_request.dart';

class BookingRemoteDatasource {
  BookingRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<BookingFareEstimate> estimateFare(
    String accessToken, {
    required int vehicleId,
    required int serviceTypeId,
    required BookingLocation pickup,
    required BookingLocation destination,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.bookingEstimate,
        data: {
          ApiKeys.vehicleId: vehicleId,
          ApiKeys.serviceTypeId: serviceTypeId,
          ApiKeys.pickupLatitude: pickup.latitude,
          ApiKeys.pickupLongitude: pickup.longitude,
          ApiKeys.destinationLatitude: destination.latitude,
          ApiKeys.destinationLongitude: destination.longitude,
        },
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      return BookingFareEstimate.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map && data[ApiKeys.detail] != null) {
        throw BookingApiException(data[ApiKeys.detail].toString());
      }
      throw const BookingApiException(
        'Không thể tính tuyến đường. Vui lòng thử lại.',
      );
    }
  }

  Future<BookingResponse> createBooking(
    String accessToken,
    CreateBookingRequest request,
  ) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.bookings,
        data: request.toJson(),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      return BookingResponse.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on FormatException {
      throw const BookingApiException(BookingStrings.sessionExpired);
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

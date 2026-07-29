import 'package:dio/dio.dart';

import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../models/driver_trip_request_model.dart';

class DriverTripRequestRemoteDatasource {
  DriverTripRequestRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<List<DriverTripRequestModel>> getOpenTripRequests(
    String accessToken,
  ) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.driverTripRequests,
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      final List data = response.data is List
          ? response.data as List
          : const [];
      return data
          .map(
            (item) => DriverTripRequestModel.fromJson(
              Map<String, dynamic>.from(item as Map),
            ),
          )
          .toList();
    } on FormatException {
      throw const DriverTripRequestApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        final detail = data[ApiKeys.detail]?.toString();
        final message = data[ApiKeys.message]?.toString();
        if (detail != null && detail.isNotEmpty) {
          throw DriverTripRequestApiException(detail);
        }
        if (message != null && message.isNotEmpty) {
          throw DriverTripRequestApiException(message);
        }
      }

      throw const DriverTripRequestApiException(
        'Không thể tải yêu cầu chuyến. Vui lòng thử lại.',
      );
    }
  }
}

class DriverTripRequestApiException implements Exception {
  const DriverTripRequestApiException(this.message);

  final String message;

  @override
  String toString() => message;
}

import 'package:dio/dio.dart';
import 'package:safe_ride/core/constants/app_strings.dart';
import 'package:safe_ride/core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../models/history_trip.dart';

class HistoryRemoteDatasource {
  HistoryRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  static const _loadErrorMessage =
      'Kh\u00f4ng th\u1ec3 t\u1ea3i l\u1ecbch s\u1eed chuy\u1ebfn \u0111i. Vui l\u00f2ng th\u1eed l\u1ea1i.';

  final Dio _dio;

  Future<List<HistoryTrip>> getBookingHistory(
    String accessToken, {
    String? role,
  }) async {
    final normalizedRole = role == AppValues.roleDriver
        ? AppValues.roleDriver
        : AppValues.roleCustomer;

    try {
      final response = await _dio.get(
        ApiEndpoints.bookingHistory,
        queryParameters: {'role': normalizedRole},
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      final List data = response.data is List
          ? response.data as List
          : const [];
      return data
          .map(
            (json) =>
                HistoryTrip.fromJson(Map<String, dynamic>.from(json as Map)),
          )
          .toList();
    } on FormatException {
      throw const HistoryApiException(BookingStrings.sessionExpired);
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map) {
        if (data[ApiKeys.detail] != null) {
          throw HistoryApiException(data[ApiKeys.detail].toString());
        }

        if (data[ApiKeys.message] != null) {
          throw HistoryApiException(data[ApiKeys.message].toString());
        }

        if (data['title'] != null) {
          throw HistoryApiException(data['title'].toString());
        }
      }

      throw const HistoryApiException(_loadErrorMessage);
    }
  }
}

class HistoryApiException implements Exception {
  const HistoryApiException(this.message);

  final String message;

  @override
  String toString() => message;
}

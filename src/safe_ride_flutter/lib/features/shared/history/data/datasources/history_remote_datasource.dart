import 'package:dio/dio.dart';
import '../../../../core/network/auth_header.dart';
import '../../../../core/network/dio_client.dart';
import '../../../../core/constants/app_strings.dart';
import '../models/history_trip.dart';

class HistoryRemoteDatasource {
  HistoryRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<List<HistoryTrip>> getBookingHistory(String accessToken) async {
    // API Code (Commented out as requested until API is ready)
    /*
    try {
      final response = await _dio.get(
        ApiEndpoints.bookings,
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );
      
      final List data = response.data as List;
      return data.map((json) => HistoryTrip.fromJson(Map<String, dynamic>.from(json))).toList();
    } on DioException catch (exception) {
      final data = exception.response?.data;
      if (data is Map && data[ApiKeys.detail] != null) {
        throw Exception(data[ApiKeys.detail].toString());
      }
      throw Exception('Không thể tải lịch sử chuyến đi.');
    }
    */
    
    // Returning empty list or throwing to let provider use mock data
    return [];
  }
}

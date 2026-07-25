import 'package:dio/dio.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/network/auth_header.dart';
import '../../../../../core/network/dio_client.dart';
import '../models/driver_rating_summary.dart';

class FeedbackRemoteDatasource {
  FeedbackRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<DriverRatingSummary> getDriverRatings(
    String accessToken, {
    required String driverId,
  }) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.getDriverRatings(driverId),
        options: Options(
          headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        ),
      );

      if (response.data is Map) {
        return DriverRatingSummary.fromJson(
          Map<String, dynamic>.from(response.data as Map),
        );
      }
      throw Exception('Invalid response format');
    } on DioException catch (e) {
      if (e.response?.statusCode == 404) {
         // If no ratings yet, backend might return 404 or empty object.
         // Assuming backend returns an empty summary if no ratings.
         // If it returns 404, we handle it as empty.
         return DriverRatingSummary(
           driverId: driverId,
           averageRating: 0,
           totalRatings: 0,
           ratings: []
         );
      }
      rethrow;
    }
  }
}

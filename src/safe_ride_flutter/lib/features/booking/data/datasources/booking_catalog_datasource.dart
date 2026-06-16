import 'package:dio/dio.dart';

import '../../../../core/constants/app_strings.dart';
import '../../../../core/network/auth_header.dart';
import '../../../../core/network/dio_client.dart';
import '../models/booking_catalog.dart';

class BookingCatalogDatasource {
  BookingCatalogDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  final Dio _dio;

  Future<BookingCatalog> getCatalog(String accessToken) async {
    final response = await _dio.get(
      ApiEndpoints.bookingCatalog,
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
      ),
    );

    return BookingCatalog.fromJson(Map<String, dynamic>.from(response.data));
  }
}

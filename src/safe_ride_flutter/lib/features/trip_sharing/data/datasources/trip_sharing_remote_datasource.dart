import 'package:dio/dio.dart';

import '../../../../core/network/auth_header.dart';
import '../../../../core/network/dio_client.dart';
import '../models/trip_share_models.dart';

class TripSharingRemoteDatasource {
  TripSharingRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;
  final Dio _dio;

  Future<CreatedTripShare> create(
    String token, {
    required int tripId,
    required String recipientPhoneNumber,
  }) async {
    final response = await _request(
      () => _dio.post(
        '/trips/$tripId/shares',
        data: {'recipientPhoneNumber': recipientPhoneNumber},
        options: _auth(token),
      ),
    );
    return CreatedTripShare.fromJson(_map(response.data));
  }

  Future<List<TripShareListItem>> list(String token, int tripId) async {
    final response = await _request(
      () => _dio.get('/trips/$tripId/shares', options: _auth(token)),
    );
    return (response.data as List)
        .map((item) => TripShareListItem.fromJson(_map(item)))
        .toList();
  }

  Future<void> revoke(String token, int tripId, int tripShareId) => _request(
    () => _dio.delete(
      '/trips/$tripId/shares/$tripShareId',
      options: _auth(token),
    ),
  );

  Future<ResolvedTripShare> resolve(String token, String rawToken) async {
    final response = await _request(
      () => _dio.post(
        '/trip-shares/resolve',
        data: {'token': rawToken},
        options: _auth(token),
      ),
    );
    return ResolvedTripShare.fromJson(_map(response.data));
  }

  Future<SharedTripTracking> tracking(String token, int tripShareId) async {
    final response = await _request(
      () =>
          _dio.get('/trip-shares/$tripShareId/tracking', options: _auth(token)),
    );
    return SharedTripTracking.fromJson(_map(response.data));
  }

  Options _auth(String token) =>
      Options(headers: {'Authorization': AuthHeader.bearer(token)});

  static Map<String, dynamic> _map(Object? data) =>
      Map<String, dynamic>.from(data as Map);

  Future<T> _request<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on DioException catch (error) {
      final data = error.response?.data;
      final detail = data is Map ? data['detail']?.toString() : null;
      throw TripSharingApiException(
        detail ?? 'Không thể xử lý chia sẻ chuyến đi. Vui lòng thử lại.',
        statusCode: error.response?.statusCode,
      );
    }
  }
}

class TripSharingApiException implements Exception {
  const TripSharingApiException(this.message, {this.statusCode});
  final String message;
  final int? statusCode;
  @override
  String toString() => message;
}

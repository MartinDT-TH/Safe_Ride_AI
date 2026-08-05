import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../../../../core/constants/app_strings.dart';
import '../../../../core/network/dio_client.dart';
import '../models/trip_share_models.dart';

class TripSharingRemoteDatasource {
  TripSharingRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;
  final Dio _dio;

  Future<CreatedTripShare> create({
    required int tripId,
    required String recipientPhoneNumber,
  }) async {
    final path = '/trips/$tripId/shares';
    final options = Options(extra: {ApiKeys.requiresAuth: true});
    _debugLog(
      'TripSharing.create request path=$path '
      'extra=${options.extra} headers=${_safeHeaders(options.headers)}',
    );
    final response = await _request(
      () => _dio.post(
        path,
        data: {'recipientPhoneNumber': recipientPhoneNumber},
        options: options,
      ),
    );
    _debugLog(
      'TripSharing response path=$path status=${response.statusCode ?? 'none'}',
    );
    return CreatedTripShare.fromJson(_map(response.data));
  }

  Future<List<TripShareListItem>> list(int tripId) async {
    final response = await _request(
      () => _dio.get(
        '/trips/$tripId/shares',
        options: Options(extra: {ApiKeys.requiresAuth: true}),
      ),
    );
    return (response.data as List)
        .map((item) => TripShareListItem.fromJson(_map(item)))
        .toList();
  }

  Future<void> revoke(int tripId, int tripShareId) => _request(
    () => _dio.delete(
      '/trips/$tripId/shares/$tripShareId',
      options: Options(extra: {ApiKeys.requiresAuth: true}),
    ),
  );

  Future<ResolvedTripShare> resolve(String rawToken) async {
    final response = await _request(
      () => _dio.post(
        '/trip-shares/resolve',
        data: {'token': rawToken},
        options: Options(extra: {ApiKeys.requiresAuth: true}),
      ),
    );
    return ResolvedTripShare.fromJson(_map(response.data));
  }

  Future<SharedTripTracking> tracking(int tripShareId) async {
    final response = await _request(
      () => _dio.get(
        '/trip-shares/$tripShareId/tracking',
        options: Options(extra: {ApiKeys.requiresAuth: true}),
      ),
    );
    return SharedTripTracking.fromJson(_map(response.data));
  }

  Future<List<ReceivedTripShare>> received({bool activeOnly = true}) async {
    final response = await _request(
      () => _dio.get(
        '/trip-shares/received',
        queryParameters: {'activeOnly': activeOnly},
        options: Options(extra: {ApiKeys.requiresAuth: true}),
      ),
    );
    return (response.data as List)
        .map((item) => ReceivedTripShare.fromJson(_map(item)))
        .toList();
  }

  static Map<String, dynamic> _map(Object? data) =>
      Map<String, dynamic>.from(data as Map);

  Future<T> _request<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on DioException catch (error) {
      final data = error.response?.data;
      final detail = data is Map ? data['detail']?.toString() : null;
      final code = data is Map ? data[ApiKeys.code]?.toString() : null;
      final traceId = data is Map ? data['traceId']?.toString() : null;
      _debugLog(
        'TripSharing response path=${error.requestOptions.path} '
        'status=${error.response?.statusCode ?? 'none'} '
        'code=${code ?? 'unknown'} traceId=${traceId ?? 'unknown'} '
        'detail=${detail ?? 'none'}',
      );
      throw TripSharingApiException(
        detail ?? 'Không thể xử lý chia sẻ chuyến đi. Vui lòng thử lại.',
        statusCode: error.response?.statusCode,
        code: code,
        traceId: traceId,
      );
    }
  }

  static Map<String, dynamic> _safeHeaders(Map<String, dynamic>? headers) {
    if (headers == null || headers.isEmpty) return const {};
    return headers.map(
      (key, value) => MapEntry(
        key,
        key.toLowerCase() == ApiKeys.authorization.toLowerCase()
            ? '<redacted>'
            : value,
      ),
    );
  }

  static void _debugLog(String message) {
    if (kDebugMode) {
      debugPrint(message);
    }
  }
}

class TripSharingApiException implements Exception {
  const TripSharingApiException(
    this.message, {
    this.statusCode,
    this.code,
    this.traceId,
  });
  final String message;
  final int? statusCode;
  final String? code;
  final String? traceId;
  @override
  String toString() => message;
}

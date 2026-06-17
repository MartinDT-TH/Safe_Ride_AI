import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';

import '../constants/app_strings.dart';
import '../storage/secure_storage_service.dart';
import 'auth_header.dart';

class AuthTokenRefreshInterceptor extends Interceptor {
  AuthTokenRefreshInterceptor({
    required Dio refreshClient,
    SecureStorageService? storage,
  }) : _refreshClient = refreshClient,
       _storage = storage ?? SecureStorageService();

  static const _retriedKey = 'auth_refresh_retried';
  static Future<String?>? _refreshInFlight;

  final Dio _refreshClient;
  final SecureStorageService _storage;

  @override
  void onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    try {
      if (_hasAuthorization(options) && !_isAuthEndpoint(options.path)) {
        final accessToken = await _readUsableAccessToken();
        if (accessToken != null) {
          options.headers[ApiKeys.authorization] = AuthHeader.bearer(
            accessToken,
          );
        }
      }
      handler.next(options);
    } catch (error) {
      handler.reject(DioException(requestOptions: options, error: error));
    }
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) async {
    final response = err.response;
    final request = err.requestOptions;

    if (response?.statusCode != 401 ||
        request.extra[_retriedKey] == true ||
        _isAuthEndpoint(request.path)) {
      handler.next(err);
      return;
    }

    try {
      final accessToken = await _refreshAccessToken();
      if (accessToken == null) {
        handler.next(err);
        return;
      }

      final retryResponse = await _retryWithAccessToken(request, accessToken);
      handler.resolve(retryResponse);
    } catch (_) {
      handler.next(err);
    }
  }

  bool _hasAuthorization(RequestOptions options) {
    return options.headers.keys.any(
      (key) => key.toLowerCase() == ApiKeys.authorization.toLowerCase(),
    );
  }

  bool _isAuthEndpoint(String path) {
    final normalizedPath = path.startsWith('/') ? path : '/$path';
    return normalizedPath == ApiEndpoints.refreshToken ||
        normalizedPath == ApiEndpoints.logout ||
        normalizedPath == ApiEndpoints.sendOtp ||
        normalizedPath == ApiEndpoints.verifyOtp ||
        normalizedPath == ApiEndpoints.googleLogin;
  }

  Future<String?> _readUsableAccessToken() async {
    final savedToken = await _storage.readAccessToken();
    if (savedToken == null || savedToken.trim().isEmpty) return null;

    final accessToken = AuthHeader.normalizeAccessToken(savedToken);
    if (!AuthHeader.isCompactJwt(accessToken)) return null;

    if (_expiresSoon(accessToken)) {
      return _refreshAccessToken();
    }

    return accessToken;
  }

  Future<String?> _refreshAccessToken() {
    final inFlight = _refreshInFlight;
    if (inFlight != null) return inFlight;

    final future = _refreshAccessTokenCore();
    _refreshInFlight = future;
    return future.whenComplete(() {
      if (identical(_refreshInFlight, future)) {
        _refreshInFlight = null;
      }
    });
  }

  Future<String?> _refreshAccessTokenCore() async {
    final refreshToken = await _storage.readRefreshToken();
    if (refreshToken == null || refreshToken.trim().isEmpty) {
      return null;
    }

    final deviceId = await _storage.readDeviceId();
    try {
      final response = await _refreshClient.post<Map<String, dynamic>>(
        ApiEndpoints.refreshToken,
        data: {
          ApiKeys.refreshToken: refreshToken,
          if (deviceId != null && deviceId.isNotEmpty)
            ApiKeys.deviceId: deviceId,
        },
      );

      final data = response.data;
      if (data == null) return null;

      final rawAccessToken = data[ApiKeys.accessToken]?.toString();
      final newRefreshToken = data[ApiKeys.refreshToken]?.toString();
      if (rawAccessToken == null ||
          newRefreshToken == null ||
          newRefreshToken.isEmpty) {
        return null;
      }

      final accessToken = AuthHeader.normalizeAccessToken(rawAccessToken);
      if (!AuthHeader.isCompactJwt(accessToken)) return null;

      await _storage.saveTokens(
        accessToken: accessToken,
        refreshToken: newRefreshToken,
      );
      return accessToken;
    } on DioException catch (error) {
      if (error.response?.statusCode == 401) {
        await _storage.clearTokens();
      }
      return null;
    }
  }

  Future<Response<dynamic>> _retryWithAccessToken(
    RequestOptions request,
    String accessToken,
  ) {
    final headers = Map<String, dynamic>.from(request.headers)
      ..[ApiKeys.authorization] = AuthHeader.bearer(accessToken);
    final extra = Map<String, dynamic>.from(request.extra)
      ..[_retriedKey] = true;

    return _refreshClient.request<dynamic>(
      request.path,
      data: request.data,
      queryParameters: request.queryParameters,
      options: Options(
        method: request.method,
        headers: headers,
        responseType: request.responseType,
        contentType: request.contentType,
        extra: extra,
        followRedirects: request.followRedirects,
        receiveDataWhenStatusError: request.receiveDataWhenStatusError,
        validateStatus: request.validateStatus,
      ),
      cancelToken: request.cancelToken,
      onReceiveProgress: request.onReceiveProgress,
      onSendProgress: request.onSendProgress,
    );
  }

  bool _expiresSoon(String accessToken) {
    final expiresAt = _readExpiry(accessToken);
    if (expiresAt == null) return false;
    return expiresAt.isBefore(
      DateTime.now().toUtc().add(const Duration(seconds: 30)),
    );
  }

  DateTime? _readExpiry(String accessToken) {
    try {
      final payload = accessToken.split('.')[1];
      final normalized = base64Url.normalize(payload);
      final decoded = utf8.decode(base64Url.decode(normalized));
      final json = jsonDecode(decoded);
      if (json is! Map<String, dynamic>) return null;
      final exp = json['exp'];
      if (exp is! num) return null;
      return DateTime.fromMillisecondsSinceEpoch(
        exp.toInt() * 1000,
        isUtc: true,
      );
    } catch (_) {
      return null;
    }
  }
}

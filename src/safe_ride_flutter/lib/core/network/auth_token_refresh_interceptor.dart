import 'package:dio/dio.dart';

import '../constants/app_strings.dart';
import '../session/session_manager.dart';
import 'auth_header.dart';

class AuthTokenRefreshInterceptor extends Interceptor {
  AuthTokenRefreshInterceptor({
    required Dio retryClient,
    required SessionManager sessionManager,
  })  : _retryClient = retryClient,
        _sessionManager = sessionManager;

  static const _retriedKey = 'auth_refresh_retried';

  final Dio _retryClient;
  final SessionManager _sessionManager;

  @override
  void onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    try {
      if (_hasAuthorization(options) && !_isAuthEndpoint(options.path)) {
        final accessToken = await _sessionManager.getValidAccessToken();
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

    final code = _extractErrorCode(response?.data);
    if (_sessionManager.isTerminalAuthCode(code)) {
      await _sessionManager.clearSession(notify: true);
      handler.next(err);
      return;
    }

    try {
      final accessToken = await _sessionManager.getValidAccessToken(
        forceRefresh: true,
      );
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

  Future<Response<dynamic>> _retryWithAccessToken(
    RequestOptions request,
    String accessToken,
  ) {
    final headers = Map<String, dynamic>.from(request.headers)
      ..[ApiKeys.authorization] = AuthHeader.bearer(accessToken);
    final extra = Map<String, dynamic>.from(request.extra)
      ..[_retriedKey] = true;

    return _retryClient.request<dynamic>(
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

  String? _extractErrorCode(Object? data) {
    if (data is Map && data[ApiKeys.code] != null) {
      return data[ApiKeys.code].toString();
    }
    return null;
  }
}

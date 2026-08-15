import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../constants/app_strings.dart';
import '../session/session_manager.dart';
import 'auth_header.dart';

class AuthTokenRefreshInterceptor extends Interceptor {
  AuthTokenRefreshInterceptor({
    required Dio retryClient,
    required SessionManager sessionManager,
  }) : _retryClient = retryClient,
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
      final requiresAuth = _requiresAuth(options);
      _debugLog('Auth request path=${options.path} requiresAuth=$requiresAuth');
      if (requiresAuth && !_isAuthEndpoint(options.path)) {
        final accessToken = await _sessionManager.getValidAccessToken();
        if (accessToken != null && accessToken.isNotEmpty) {
          _setAuthorizationHeader(options.headers, accessToken);
          _debugLog(
            'Authorization attached for ${options.path} '
            'token=${_fingerprint(accessToken)} '
            'authorizationHeaderCount=${_authorizationHeaderCount(options.headers)}',
          );
        } else {
          _debugLog(
            'Authorization not attached for ${options.path}: '
            'no valid access token',
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
    final code = _extractErrorCode(response?.data);
    if (_sessionManager.isTerminalAuthCode(code)) {
      await _sessionManager.clearSession(
        notify: true,
        reasonMessage: _extractErrorMessage(response?.data),
      );
      handler.next(err);
      return;
    }

    if (response?.statusCode != 401 ||
        request.extra[_retriedKey] == true ||
        _isAuthEndpoint(request.path) ||
        !_requiresAuth(request)) {
      handler.next(err);
      return;
    }

    final traceId = _extractTraceId(response?.data);
    _debugLog(
      'Auth 401 path=${request.path} code=${code ?? 'unknown'} '
      'traceId=${traceId ?? 'unknown'}; refreshing token',
    );
    if (_sessionManager.isTerminalAuthCode(code)) {
      await _sessionManager.clearSession(notify: true);
      handler.next(err);
      return;
    }

    try {
      final accessToken = await _sessionManager.getValidAccessToken(
        forceRefresh: true,
      );
      if (accessToken == null || accessToken.isEmpty) {
        _debugLog('Refresh failed or session expired for ${request.path}');
        handler.next(err);
        return;
      }

      _debugLog(
        'Retrying ${request.path} once with refreshed '
        'token=${_fingerprint(accessToken)}',
      );
      final retryResponse = await _retryWithAccessToken(request, accessToken);
      handler.resolve(retryResponse);
    } on DioException catch (error) {
      _debugLog(
        'Retry failed path=${request.path} '
        'status=${error.response?.statusCode ?? 'none'} '
        'code=${_extractErrorCode(error.response?.data) ?? 'unknown'} '
        'traceId=${_extractTraceId(error.response?.data) ?? 'unknown'}',
      );
      handler.next(err);
    } catch (error) {
      _debugLog('Retry after refresh failed for ${request.path}: $error');
      handler.next(err);
    }
  }

  bool _requiresAuth(RequestOptions options) {
    return options.extra[ApiKeys.requiresAuth] == true ||
        _hasAuthorization(options);
  }

  bool _hasAuthorization(RequestOptions options) {
    return _authorizationHeaderCount(options.headers) > 0;
  }

  int _authorizationHeaderCount(Map<String, dynamic> headers) {
    return headers.keys
        .where(
          (key) => key.toLowerCase() == ApiKeys.authorization.toLowerCase(),
        )
        .length;
  }

  void _setAuthorizationHeader(
    Map<String, dynamic> headers,
    String accessToken,
  ) {
    headers.removeWhere(
      (key, _) => key.toLowerCase() == ApiKeys.authorization.toLowerCase(),
    );
    headers[ApiKeys.authorization] = AuthHeader.bearer(accessToken);
  }

  String _fingerprint(String? accessToken) {
    if (accessToken == null || accessToken.length < 16) {
      return 'none';
    }
    return '${accessToken.substring(0, 8)}...${accessToken.substring(accessToken.length - 8)}';
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
      ..removeWhere(
        (key, _) => key.toLowerCase() == ApiKeys.authorization.toLowerCase(),
      )
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

  String? _extractTraceId(Object? data) {
    if (data is Map && data['traceId'] != null) {
      return data['traceId'].toString();
    }
    return null;
  }

  String? _extractErrorMessage(Object? data) {
    if (data is Map) {
      final detail = data['detail'] ?? data['message'] ?? data['title'];
      return detail?.toString();
    }
    return null;
  }

  void _debugLog(String message) {
    if (kDebugMode) {
      debugPrint(message);
    }
  }
}

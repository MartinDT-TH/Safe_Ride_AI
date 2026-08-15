import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../constants/app_strings.dart';
import 'auth_token_refresh_interceptor.dart';
import '../../dependency_injection/injection.dart';
import '../session/session_manager.dart';
import '../services/connectivity_service.dart';

class DioClient {
  factory DioClient() => _instance;

  DioClient._();

  static final DioClient _instance = DioClient._();

  static final Dio _refreshDio = Dio(
    BaseOptions(
      baseUrl: AppConfig.apiBaseUrl,
      connectTimeout: const Duration(seconds: 10),
      receiveTimeout: const Duration(seconds: 30),
    ),
  );

  static final Dio _dio = _createDio();

  Dio get dio => _dio;

  static Dio _createDio() {
    final dio = Dio(
      BaseOptions(
        baseUrl: AppConfig.apiBaseUrl,
        connectTimeout: const Duration(seconds: 10),
        receiveTimeout: const Duration(seconds: 30),
      ),
    );

    dio.interceptors.add(
      AuthTokenRefreshInterceptor(
        retryClient: _refreshDio,
        sessionManager: getIt<SessionManager>(),
      ),
    );

    dio.interceptors.add(DioErrorInterceptor());

    if (kDebugMode) {
      dio.interceptors.add(
        LogInterceptor(
          requestHeader: false,
          requestBody: false,
          responseHeader: false,
          responseBody: false,
        ),
      );
    }

    return dio;
  }
}

abstract final class DioRequestExtras {
  static const suppressGlobalErrorSnackBar = 'suppressGlobalErrorSnackBar';
}

class DioErrorInterceptor extends Interceptor {
  DioErrorInterceptor({ConnectivityService? connectivityService})
    : _injectedConnectivityService = connectivityService;

  final ConnectivityService? _injectedConnectivityService;

  ConnectivityService get _connectivityService =>
      _injectedConnectivityService ?? getIt<ConnectivityService>();

  @override
  void onResponse(Response response, ResponseInterceptorHandler handler) {
    final statusCode = response.statusCode;
    final suppressNotice =
        response.requestOptions.extra[DioRequestExtras
            .suppressGlobalErrorSnackBar] ==
        true;
    if (statusCode != null && statusCode >= 500) {
      if (!suppressNotice) {
        _connectivityService.reportServerUnavailable(autoRetry: false);
      }
    } else {
      _connectivityService.reportServerReachable();
    }
    handler.next(response);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) async {
    await handleError(err);
    handler.next(err);
  }

  @visibleForTesting
  Future<void> handleError(DioException err) async {
    final statusCode = err.response?.statusCode;
    if (statusCode != null && statusCode < 500) {
      // A 4xx response is still proof that the API is reachable.
      _connectivityService.reportServerReachable();
      return;
    }

    final isTransportFailure =
        err.type == DioExceptionType.connectionTimeout ||
        err.type == DioExceptionType.receiveTimeout ||
        err.type == DioExceptionType.sendTimeout ||
        err.type == DioExceptionType.connectionError;
    final isServerFailure = statusCode != null && statusCode >= 500;

    if (!isTransportFailure && !isServerFailure) return;

    if (isTransportFailure) {
      final isOffline = await _connectivityService.refreshNetworkStatus();
      if (isOffline) return;
    }

    if (err.requestOptions.extra[DioRequestExtras
            .suppressGlobalErrorSnackBar] ==
        true) {
      return;
    }

    _connectivityService.reportServerUnavailable(autoRetry: isTransportFailure);
  }
}

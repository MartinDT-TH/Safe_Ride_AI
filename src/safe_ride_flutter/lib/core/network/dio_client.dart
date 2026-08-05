import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../constants/app_strings.dart';
import '../localization/locale_provider.dart';
import 'auth_token_refresh_interceptor.dart';
import '../../dependency_injection/injection.dart';
import '../session/session_manager.dart';
import '../services/connectivity_service.dart';
import '../widgets/app_snackbar.dart';

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
  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    _handleError(err);
    super.onError(err, handler);
  }

  void _handleError(DioException err) {
    if (err.requestOptions.extra[DioRequestExtras
            .suppressGlobalErrorSnackBar] ==
        true) {
      return;
    }

    final isServerError =
        err.type == DioExceptionType.connectionTimeout ||
        err.type == DioExceptionType.receiveTimeout ||
        err.type == DioExceptionType.sendTimeout ||
        err.type == DioExceptionType.connectionError ||
        (err.response != null &&
            err.response!.statusCode != null &&
            err.response!.statusCode! >= 500);

    if (isServerError) {
      // Use getIt to get ConnectivityService and show the notification globally
      try {
        final connectivityService = getIt<ConnectivityService>();

        final l10n = LocaleProvider.currentLocalizations;
        AppSnackBar.showGlobal(
          connectivityService.messengerKey,
          message: l10n.serverConnectionError,
          type: AppSnackBarType.serverError,
          title: l10n.serverConnectionErrorTitle,
          actionLabel: l10n.dismiss,
          onAction: () {},
        );
      } catch (e) {
        debugPrint('Cannot show global server error: $e');
      }
    }
  }
}

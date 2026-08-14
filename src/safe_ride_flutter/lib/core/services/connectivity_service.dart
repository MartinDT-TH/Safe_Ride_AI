import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';

import '../constants/app_strings.dart';
import '../localization/locale_provider.dart';
import '../widgets/app_snackbar.dart';

enum ConnectionStatus { unknown, online, offline, serverUnavailable }

abstract interface class NetworkConnectivity {
  Future<List<ConnectivityResult>> checkConnectivity();

  Stream<List<ConnectivityResult>> get onConnectivityChanged;
}

class ConnectivityPlusNetworkConnectivity implements NetworkConnectivity {
  ConnectivityPlusNetworkConnectivity([Connectivity? connectivity])
    : _connectivity = connectivity ?? Connectivity();

  final Connectivity _connectivity;

  @override
  Future<List<ConnectivityResult>> checkConnectivity() {
    return _connectivity.checkConnectivity();
  }

  @override
  Stream<List<ConnectivityResult>> get onConnectivityChanged {
    return _connectivity.onConnectivityChanged;
  }
}

abstract interface class ServerReachability {
  Future<bool> check();
}

class DioServerReachability implements ServerReachability {
  DioServerReachability({Dio? dio, Uri? healthUri})
    : _dio =
          dio ??
          Dio(
            BaseOptions(
              connectTimeout: const Duration(seconds: 5),
              receiveTimeout: const Duration(seconds: 5),
            ),
          ),
      _healthUri = healthUri ?? _resolveHealthUri(AppConfig.apiBaseUrl);

  final Dio _dio;
  final Uri _healthUri;

  static Uri _resolveHealthUri(String apiBaseUrl) {
    final apiUri = Uri.parse(apiBaseUrl);
    return apiUri.replace(path: '/health', query: null, fragment: null);
  }

  @override
  Future<bool> check() async {
    try {
      final response = await _dio.getUri<Object?>(
        _healthUri,
        options: Options(validateStatus: (status) => status != null),
      );
      final statusCode = response.statusCode;
      return statusCode != null && statusCode < 500;
    } catch (_) {
      return false;
    }
  }
}

enum _ConnectionNotice {
  offline,
  serverUnavailable,
  internetRestored,
  serverRestored,
}

class ConnectivityService extends ChangeNotifier {
  ConnectivityService({
    NetworkConnectivity? connectivity,
    ServerReachability? serverReachability,
    Duration networkRetryInterval = const Duration(seconds: 5),
    Duration serverRetryInterval = const Duration(seconds: 5),
  }) : _connectivity = connectivity ?? ConnectivityPlusNetworkConnectivity(),
       _serverReachability = serverReachability ?? DioServerReachability(),
       _networkRetryInterval = networkRetryInterval,
       _serverRetryInterval = serverRetryInterval;

  final NetworkConnectivity _connectivity;
  final ServerReachability _serverReachability;
  final Duration _networkRetryInterval;
  final Duration _serverRetryInterval;
  StreamSubscription<List<ConnectivityResult>>? _subscription;
  Timer? _networkRetryTimer;
  Timer? _serverRetryTimer;
  bool _serverCheckInFlight = false;
  bool _autoRetryServer = false;

  final StreamController<void> _reloadRequestController =
      StreamController<void>.broadcast();
  Stream<void> get reloadRequests => _reloadRequestController.stream;

  ScaffoldFeatureController<SnackBar, SnackBarClosedReason>?
  _connectionSnackBarController;
  _ConnectionNotice? _visibleNotice;

  final GlobalKey<ScaffoldMessengerState> messengerKey =
      GlobalKey<ScaffoldMessengerState>();

  ConnectionStatus _status = ConnectionStatus.unknown;
  ConnectionStatus get status => _status;
  bool get isOffline => _status == ConnectionStatus.offline;

  bool _initialized = false;
  bool _disposed = false;

  void initialize() {
    if (_initialized) return;
    _initialized = true;

    WidgetsBinding.instance.addPostFrameCallback((_) {
      unawaited(refreshNetworkStatus(isInitial: true));
    });

    _subscription = _connectivity.onConnectivityChanged.listen(
      _handleConnectivityChange,
      onError: (Object error, StackTrace stackTrace) {
        debugPrint('Connectivity listener failed: $error');
      },
    );
  }

  Future<bool> refreshNetworkStatus({bool isInitial = false}) async {
    try {
      final results = await _connectivity.checkConnectivity();
      _handleConnectivityChange(results, isInitial: isInitial);
    } catch (error) {
      debugPrint('Connectivity check failed: $error');
      if (isOffline) {
        _scheduleNetworkRetry();
      }
    }

    return isOffline;
  }

  void reportServerUnavailable({bool autoRetry = true}) {
    if (isOffline) return;
    _autoRetryServer = _autoRetryServer || autoRetry;
    _setStatus(ConnectionStatus.serverUnavailable);
    _showServerUnavailableSnackBar();
    if (_autoRetryServer) {
      _scheduleServerRetry();
    }
  }

  void reportServerReachable() {
    if (isOffline) return;
    final wasServerUnavailable = _status == ConnectionStatus.serverUnavailable;
    _setStatus(ConnectionStatus.online);
    if (wasServerUnavailable) {
      _cancelServerRetry();
      _showServerRestoredSnackBar();
      _requestReload();
    }
  }

  Future<bool> retryServerConnection() async {
    if (_disposed || isOffline || _serverCheckInFlight) return false;

    _serverCheckInFlight = true;
    try {
      final isReachable = await _serverReachability.check();
      if (isReachable) {
        reportServerReachable();
        return true;
      }

      if (_status == ConnectionStatus.serverUnavailable) {
        _showServerUnavailableSnackBar();
        _scheduleServerRetry();
      }
      return false;
    } finally {
      _serverCheckInFlight = false;
    }
  }

  Future<void> handleRealtimeConnectionLost() async {
    final offline = await refreshNetworkStatus();
    if (!offline) {
      reportServerUnavailable();
    }
  }

  void _handleConnectivityChange(
    List<ConnectivityResult> results, {
    bool isInitial = false,
  }) {
    if (_disposed) return;

    final wasOffline = isOffline;
    final isNowOffline =
        results.isEmpty ||
        results.every((result) => result == ConnectivityResult.none);

    if (isNowOffline) {
      _cancelServerRetry();
      _setStatus(ConnectionStatus.offline);
      _showNoInternetSnackBar();
      _scheduleNetworkRetry();
      return;
    }

    _cancelNetworkRetry();
    if (wasOffline) {
      _setStatus(ConnectionStatus.online);
      if (!isInitial) {
        _showBackOnlineSnackBar();
        _requestReload();
      }
      return;
    }

    // An available network interface does not prove that the API is reachable.
    // Preserve serverUnavailable until a real API response confirms recovery.
    if (_status == ConnectionStatus.unknown) {
      _setStatus(ConnectionStatus.online);
    }
  }

  void _setStatus(ConnectionStatus nextStatus) {
    if (_disposed || _status == nextStatus) return;
    _status = nextStatus;
    notifyListeners();
  }

  void _showBackOnlineSnackBar() {
    final l10n = LocaleProvider.currentLocalizations;
    _showConnectionSnackBar(
      notice: _ConnectionNotice.internetRestored,
      message: l10n.internetRestored,
      type: AppSnackBarType.success,
      title: l10n.backOnline,
      actionLabel: l10n.reload,
      onAction: _requestReload,
      duration: const Duration(seconds: 3),
    );
  }

  void _showNoInternetSnackBar() {
    final l10n = LocaleProvider.currentLocalizations;
    _showConnectionSnackBar(
      notice: _ConnectionNotice.offline,
      message: l10n.noInternetConnection,
      type: AppSnackBarType.error,
      title: l10n.connectionLost,
      actionLabel: l10n.reload,
      onAction: () {
        _forgetVisibleNotice();
        unawaited(refreshNetworkStatus());
      },
      duration: const Duration(days: 1),
    );
  }

  void _showServerUnavailableSnackBar() {
    final l10n = LocaleProvider.currentLocalizations;
    _showConnectionSnackBar(
      notice: _ConnectionNotice.serverUnavailable,
      message: l10n.serverConnectionError,
      type: AppSnackBarType.serverError,
      title: l10n.serverConnectionErrorTitle,
      actionLabel: l10n.reload,
      onAction: () {
        _forgetVisibleNotice();
        unawaited(retryServerConnection());
      },
      duration: const Duration(days: 1),
    );
  }

  void _showServerRestoredSnackBar() {
    final l10n = LocaleProvider.currentLocalizations;
    _showConnectionSnackBar(
      notice: _ConnectionNotice.serverRestored,
      message: l10n.serverConnectionRestored,
      type: AppSnackBarType.success,
      title: l10n.serverConnectionRestoredTitle,
      actionLabel: l10n.reload,
      onAction: _requestReload,
      duration: const Duration(seconds: 6),
    );
  }

  void _showConnectionSnackBar({
    required _ConnectionNotice notice,
    required String message,
    required AppSnackBarType type,
    required String title,
    String? actionLabel,
    VoidCallback? onAction,
    required Duration duration,
  }) {
    if (_disposed) return;

    if (_visibleNotice == notice && _connectionSnackBarController != null) {
      return;
    }

    final controller = AppSnackBar.showGlobal(
      messengerKey,
      message: message,
      type: type,
      title: title,
      actionLabel: actionLabel,
      onAction: onAction,
      duration: duration,
    );
    if (controller == null) return;

    _visibleNotice = notice;
    _connectionSnackBarController = controller;
    unawaited(
      controller.closed.whenComplete(() {
        if (identical(_connectionSnackBarController, controller)) {
          _forgetVisibleNotice();
        }
      }),
    );
  }

  void _forgetVisibleNotice() {
    _connectionSnackBarController = null;
    _visibleNotice = null;
  }

  void _scheduleServerRetry() {
    if (_disposed ||
        !_autoRetryServer ||
        isOffline ||
        _status != ConnectionStatus.serverUnavailable ||
        _serverRetryTimer?.isActive == true) {
      return;
    }

    _serverRetryTimer = Timer(_serverRetryInterval, () {
      _serverRetryTimer = null;
      unawaited(retryServerConnection());
    });
  }

  void _scheduleNetworkRetry() {
    if (_disposed || !isOffline || _networkRetryTimer?.isActive == true) {
      return;
    }

    _networkRetryTimer = Timer(_networkRetryInterval, () {
      _networkRetryTimer = null;
      unawaited(refreshNetworkStatus());
    });
  }

  void _cancelNetworkRetry() {
    _networkRetryTimer?.cancel();
    _networkRetryTimer = null;
  }

  void _cancelServerRetry() {
    _serverRetryTimer?.cancel();
    _serverRetryTimer = null;
    _autoRetryServer = false;
  }

  void _requestReload() {
    if (!_disposed && !_reloadRequestController.isClosed) {
      _reloadRequestController.add(null);
    }
  }

  @override
  void dispose() {
    if (_disposed) return;
    _disposed = true;
    _cancelNetworkRetry();
    _cancelServerRetry();
    _connectionSnackBarController?.close();
    _forgetVisibleNotice();
    unawaited(_subscription?.cancel());
    _subscription = null;
    unawaited(_reloadRequestController.close());
    super.dispose();
  }
}

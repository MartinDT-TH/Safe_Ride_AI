import 'dart:async';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';

import '../widgets/app_snackbar.dart';
import '../localization/locale_provider.dart';

class ConnectivityService {
  final Connectivity _connectivity = Connectivity();
  StreamSubscription<List<ConnectivityResult>>? _subscription;

  // Use GlobalKey to show snackbars without context
  final GlobalKey<ScaffoldMessengerState> messengerKey =
      GlobalKey<ScaffoldMessengerState>();

  bool _isFirstCheck = true;
  bool _wasOffline = false;

  void initialize() {
    // Wait for the app to be ready before showing initial status
    WidgetsBinding.instance.addPostFrameCallback((_) async {
      // Initial check
      final results = await _connectivity.checkConnectivity();
      _handleConnectivityChange(results, isInitial: true);
    });

    // Listen for changes
    _subscription = _connectivity.onConnectivityChanged.listen((
      List<ConnectivityResult> results,
    ) {
      _handleConnectivityChange(results);
    });
  }

  void _handleConnectivityChange(
    List<ConnectivityResult> results, {
    bool isInitial = false,
  }) {
    final bool isOffline = results.contains(ConnectivityResult.none);

    if (isOffline) {
      _wasOffline = true;
      _showNoInternetSnackBar();
    } else {
      if (!isInitial && _wasOffline) {
        _showBackOnlineSnackBar();
        _wasOffline = false;
      } else if (isInitial) {
        // Just to be sure we clear any potential stale state
        _wasOffline = false;
      }
    }

    _isFirstCheck = false;
  }

  void _showNoInternetSnackBar() {
    final l10n = LocaleProvider.currentLocalizations;
    AppSnackBar.showGlobal(
      messengerKey,
      message: l10n.noInternetConnection,
      type: AppSnackBarType.error,
      title: l10n.connectionLost,
      duration: const Duration(days: 1), // Keep it until online
    );
  }

  void _showBackOnlineSnackBar() {
    final l10n = LocaleProvider.currentLocalizations;
    AppSnackBar.showGlobal(
      messengerKey,
      message: l10n.internetRestored,
      type: AppSnackBarType.success,
      title: l10n.backOnline,
      duration: const Duration(seconds: 3),
    );
  }

  void dispose() {
    _subscription?.cancel();
  }
}

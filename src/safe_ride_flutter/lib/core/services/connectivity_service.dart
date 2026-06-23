import 'dart:async';
import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/material.dart';

import '../widgets/app_snackbar.dart';

class ConnectivityService {
  final Connectivity _connectivity = Connectivity();
  StreamSubscription<List<ConnectivityResult>>? _subscription;
  
  // Use GlobalKey to show snackbars without context
  final GlobalKey<ScaffoldMessengerState> messengerKey = GlobalKey<ScaffoldMessengerState>();

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
    _subscription = _connectivity.onConnectivityChanged.listen((List<ConnectivityResult> results) {
      _handleConnectivityChange(results);
    });
  }

  void _handleConnectivityChange(List<ConnectivityResult> results, {bool isInitial = false}) {
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
    AppSnackBar.showGlobal(
      messengerKey,
      message: 'Không có kết nối internet',
      type: AppSnackBarType.error,
      title: 'Mất kết nối',
      duration: const Duration(days: 1), // Keep it until online
    );
  }

  void _showBackOnlineSnackBar() {
    AppSnackBar.showGlobal(
      messengerKey,
      message: 'Đã khôi phục kết nối internet',
      type: AppSnackBarType.success,
      title: 'Đã trực tuyến',
      duration: const Duration(seconds: 3),
    );
  }

  void dispose() {
    _subscription?.cancel();
  }
}

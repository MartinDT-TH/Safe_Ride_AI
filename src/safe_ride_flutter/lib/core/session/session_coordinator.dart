import 'dart:async';

import 'package:flutter/material.dart';

import '../../features/auth/presentation/pages/login_page.dart';
import '../constants/app_strings.dart';
import '../localization/locale_provider.dart';
import 'session_manager.dart';

class SessionCoordinator {
  SessionCoordinator(this._sessionManager);

  static final GlobalKey<NavigatorState> navigatorKey =
      GlobalKey<NavigatorState>();

  final SessionManager _sessionManager;
  StreamSubscription<void>? _subscription;
  bool _isNavigating = false;

  void start() {
    _subscription ??= _sessionManager.sessionExpiredStream.listen((_) {
      _handleSessionExpired();
    });
  }

  void dispose() {
    _subscription?.cancel();
    _subscription = null;
  }

  void _handleSessionExpired() {
    if (_isNavigating) {
      return;
    }

    final navigator = navigatorKey.currentState;
    final context = navigatorKey.currentContext;
    if (navigator == null || context == null) {
      return;
    }

    _isNavigating = true;
    final message =
        _sessionManager.lastSessionEndMessage ??
        LocaleProvider.currentLocalizations.sessionExpired;
    ScaffoldMessenger.maybeOf(context)?.showSnackBar(
      SnackBar(
        content: Text(message),
        behavior: SnackBarBehavior.floating,
      ),
    );
    navigator.pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => LoginPage()),
      (route) => false,
    ).whenComplete(() {
      _isNavigating = false;
    });
  }
}

import 'dart:async';

import 'package:app_links/app_links.dart';
import 'package:flutter/material.dart';

import '../../core/services/connectivity_service.dart';
import '../../core/session/session_coordinator.dart';
import '../../core/session/session_manager.dart';
import '../../core/storage/secure_storage_service.dart';
import '../../dependency_injection/injection.dart';
import '../auth/presentation/pages/login_page.dart';
import '../auth/presentation/providers/auth_provider.dart';
import 'data/datasources/trip_sharing_remote_datasource.dart';
import 'presentation/pages/shared_trip_tracking_page.dart';

class TripShareDeepLinkCoordinator {
  TripShareDeepLinkCoordinator(
    this._storage,
    this._sessionManager,
    this._datasource,
  );

  final SecureStorageService _storage;
  final SessionManager _sessionManager;
  final TripSharingRemoteDatasource _datasource;
  final AppLinks _appLinks = AppLinks();
  StreamSubscription<Uri>? _subscription;
  AuthProvider? _auth;
  bool _processing = false;
  bool _loginOpened = false;

  Future<void> start(AuthProvider auth) async {
    _auth = auth;
    auth.addListener(_onAuthChanged);
    _subscription = _appLinks.uriLinkStream.listen(handleUri);
    final initialUri = await _appLinks.getInitialLink();
    if (initialUri != null) {
      await handleUri(initialUri);
    } else {
      await processPending();
    }
  }

  @visibleForTesting
  Future<void> handleUri(Uri uri) async {
    if (uri.host != 'app.saferide.vn' || uri.path != '/trip-share') return;
    final token = uri.queryParameters['t'];
    if (token == null || token.trim().isEmpty) return;
    await _storage.savePendingTripShareToken(token.trim());
    if (_auth != null) {
      await processPending();
    }
  }

  void _onAuthChanged() {
    if (_auth?.isRestoringSession == false && _auth?.token != null) {
      _loginOpened = false;
      unawaited(processPending());
    }
  }

  Future<void> processPending() async {
    if (_processing || _auth?.isRestoringSession == true) return;
    final pendingToken = await _storage.readPendingTripShareToken();
    if (pendingToken == null || pendingToken.isEmpty) return;

    final navigator = SessionCoordinator.navigatorKey.currentState;
    if (navigator == null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        unawaited(processPending());
      });
      return;
    }

    final accessToken = await _sessionManager.getValidAccessToken();
    if (accessToken == null || accessToken.isEmpty) {
      if (!_loginOpened) {
        _loginOpened = true;
        navigator.pushAndRemoveUntil(
          MaterialPageRoute(builder: (_) => const LoginPage()),
          (_) => false,
        );
      }
      return;
    }

    _processing = true;
    try {
      final resolved = await _datasource.resolve(accessToken, pendingToken);
      await _storage.deletePendingTripShareToken();
      navigator.push(
        MaterialPageRoute(
          builder: (_) =>
              SharedTripTrackingPage(tripShareId: resolved.tripShareId),
        ),
      );
    } on TripSharingApiException catch (error) {
      await _storage.deletePendingTripShareToken();
      getIt<ConnectivityService>().messengerKey.currentState?.showSnackBar(
        SnackBar(content: Text(error.message)),
      );
    } finally {
      _processing = false;
    }
  }

  Future<void> dispose() async {
    _auth?.removeListener(_onAuthChanged);
    await _subscription?.cancel();
  }
}

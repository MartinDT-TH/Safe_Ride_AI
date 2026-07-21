import 'dart:async';

import 'package:app_links/app_links.dart';
import 'package:flutter/material.dart';

import '../../core/services/connectivity_service.dart';
import '../../core/session/session_coordinator.dart';
import '../../core/session/session_manager.dart';
import '../../core/storage/secure_storage_service.dart';
import '../../dependency_injection/injection.dart';
import '../auth/presentation/providers/auth_provider.dart';
import 'data/datasources/trip_sharing_remote_datasource.dart';
import 'presentation/pages/shared_trip_tracking_page.dart';

/// Captures deep links at process start, but only navigates after the normal
/// Splash/Login/Profile flow has completed and an app home is on screen.
class TripShareDeepLinkCoordinator {
  TripShareDeepLinkCoordinator(
    this._storage,
    this._sessionManager,
    this._datasource,
    this._appLinkBaseUrl,
  );

  final SecureStorageService _storage;
  final SessionManager _sessionManager;
  final TripSharingRemoteDatasource _datasource;
  final String Function() _appLinkBaseUrl;
  final AppLinks _appLinks = AppLinks();
  StreamSubscription<Uri>? _subscription;
  AuthProvider? _auth;
  bool _processing = false;
  bool _navigationReady = false;
  int? _openedTripShareId;

  Future<void> start(AuthProvider auth) async {
    _auth = auth;
    _subscription = _appLinks.uriLinkStream.listen(captureUri);
    final initialUri = await _appLinks.getInitialLink();
    if (initialUri != null) await captureUri(initialUri);
  }

  @visibleForTesting
  Future<void> handleUri(Uri uri) => captureUri(uri);

  Future<void> captureUri(Uri uri) async {
    if (!matchesConfiguredAppLink(uri, _appLinkBaseUrl())) return;
    final token = uri.queryParameters['t']?.trim();
    if (token == null || token.isEmpty) return;
    await _storage.savePendingTripShareToken(token);
    if (_navigationReady) unawaited(processPendingAfterNavigation());
  }

  Future<bool> hasPendingTripShare() async {
    final token = await _storage.readPendingTripShareToken();
    return token != null && token.isNotEmpty;
  }

  /// Call this from the normal destination page after its own navigation has
  /// settled. It deliberately never opens Login or replaces an existing route.
  Future<void> processPendingAfterNavigation() async {
    _navigationReady = true;
    if (_processing || _auth?.isRestoringSession == true) return;
    final pendingToken = await _storage.readPendingTripShareToken();
    if (pendingToken == null || pendingToken.isEmpty) return;

    final accessToken = await _sessionManager.getValidAccessToken();
    if (accessToken == null || accessToken.isEmpty) return;
    final navigator = SessionCoordinator.navigatorKey.currentState;
    if (navigator == null) return;

    _processing = true;
    try {
      final resolved = await _datasource.resolve(accessToken, pendingToken);
      await _storage.deletePendingTripShareToken();
      if (_openedTripShareId == resolved.tripShareId) return;
      _openedTripShareId = resolved.tripShareId;
      unawaited(
        navigator
            .push<void>(
              MaterialPageRoute(
                builder: (_) =>
                    SharedTripTrackingPage(tripShareId: resolved.tripShareId),
              ),
            )
            .whenComplete(() => _openedTripShareId = null),
      );
    } on TripSharingApiException catch (error) {
      if (const {400, 403, 404, 410}.contains(error.statusCode)) {
        await _storage.deletePendingTripShareToken();
      }
      _showFailure(error.message);
    } catch (_) {
      _showFailure(
        'KhÃ´ng thá»ƒ má»Ÿ chuyáº¿n Ä‘i Ä‘Æ°á»£c chia sáº». Vui lÃ²ng thá»­ láº¡i.',
      );
    } finally {
      _processing = false;
    }
  }

  void _showFailure(String message) {
    getIt<ConnectivityService>().messengerKey.currentState?.showSnackBar(
      SnackBar(
        content: Text(message),
        action: SnackBarAction(
          label: 'Thá»­ láº¡i',
          onPressed: () => unawaited(processPendingAfterNavigation()),
        ),
      ),
    );
  }

  @visibleForTesting
  static bool matchesConfiguredAppLink(Uri incoming, String configuredBaseUrl) {
    final configured = Uri.tryParse(configuredBaseUrl.trim());
    if (configured == null ||
        !configured.hasScheme ||
        configured.host.isEmpty) {
      return false;
    }
    final expectedPath = configured.path.endsWith('/')
        ? '${configured.path}trip-share'
        : '${configured.path}/trip-share';
    return incoming.scheme.toLowerCase() == configured.scheme.toLowerCase() &&
        incoming.host.toLowerCase() == configured.host.toLowerCase() &&
        incoming.port == configured.port &&
        incoming.path == expectedPath;
  }

  Future<void> dispose() async {
    await _subscription?.cancel();
  }
}

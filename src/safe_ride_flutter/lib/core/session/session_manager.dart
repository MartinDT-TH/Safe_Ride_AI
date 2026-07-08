import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';

import '../constants/app_strings.dart';
import '../network/auth_header.dart';
import '../storage/secure_storage_service.dart';

class SessionTokens {
  const SessionTokens({
    required this.accessToken,
    required this.refreshToken,
    required this.sessionMode,
    required this.reloginRequiredAfterTrip,
    this.continuationTripId,
    this.continuationAbsoluteExpiresAt,
  });

  final String accessToken;
  final String refreshToken;
  final String sessionMode;
  final bool reloginRequiredAfterTrip;
  final int? continuationTripId;
  final DateTime? continuationAbsoluteExpiresAt;

  bool get isTripContinuation => sessionMode == SessionModes.tripContinuation;
}

abstract final class SessionModes {
  static const normal = 'normal';
  static const tripContinuation = 'tripContinuation';
}

class SessionManager {
  SessionManager({
    required SecureStorageService storage,
    Dio? refreshClient,
  })  : _storage = storage,
        _refreshClient = refreshClient ??
            Dio(
              BaseOptions(
                baseUrl: AppConfig.apiBaseUrl,
                connectTimeout: const Duration(seconds: 10),
                receiveTimeout: const Duration(seconds: 30),
              ),
            );

  static const _refreshWindow = Duration(seconds: 30);

  final SecureStorageService _storage;
  final Dio _refreshClient;
  final _sessionExpiredController = StreamController<void>.broadcast();
  final _tokenUpdatedController = StreamController<SessionTokens>.broadcast();
  Completer<String?>? _refreshCompleter;

  Stream<void> get sessionExpiredStream => _sessionExpiredController.stream;
  Stream<SessionTokens> get tokenUpdatedStream => _tokenUpdatedController.stream;

  Future<String?> getValidAccessToken({bool forceRefresh = false}) async {
    final savedToken = await _storage.readAccessToken();
    if (savedToken == null || savedToken.trim().isEmpty) {
      return null;
    }

    final accessToken = AuthHeader.normalizeAccessToken(savedToken);
    if (!AuthHeader.isCompactJwt(accessToken)) {
      await clearSession(notify: true);
      return null;
    }

    if (forceRefresh || _expiresSoon(accessToken)) {
      return refreshAccessToken();
    }

    return accessToken;
  }

  Future<String?> refreshAccessToken() {
    final inFlight = _refreshCompleter;
    if (inFlight != null) {
      return inFlight.future;
    }

    final completer = Completer<String?>();
    _refreshCompleter = completer;
    _refreshAccessTokenCore().then(
      completer.complete,
      onError: completer.completeError,
    ).whenComplete(() {
      if (identical(_refreshCompleter, completer)) {
        _refreshCompleter = null;
      }
    });
    return completer.future;
  }

  Future<bool> persistAuthResponse(Map<String, dynamic> response) async {
    final tokens = _readTokens(response);
    if (tokens == null) {
      return false;
    }

    await _persistTokens(tokens);
    _tokenUpdatedController.add(tokens);
    return true;
  }

  Future<void> clearSession({bool notify = true}) async {
    await _storage.clearTokens();
    if (notify) {
      _sessionExpiredController.add(null);
    }
  }

  Future<void> completeDeferredRelogin() {
    return clearSession(notify: true);
  }

  Future<bool> isTripContinuationSession() async {
    return (await _storage.readSessionMode()) == SessionModes.tripContinuation;
  }

  Future<int?> continuationTripId() {
    return _storage.readContinuationTripId();
  }

  bool isTerminalAuthCode(Object? code) {
    return switch (code?.toString()) {
      'auth.refresh_token_expired' ||
      'auth.refresh_token_reused' ||
      'auth.account_inactive' ||
      'auth.account_locked' ||
      'auth.session_expired' ||
      'auth.trip_continuation_expired' => true,
      _ => false,
    };
  }

  Future<String?> _refreshAccessTokenCore() async {
    final refreshToken = await _storage.readRefreshToken();
    if (refreshToken == null || refreshToken.trim().isEmpty) {
      await clearSession(notify: true);
      return null;
    }

    final deviceId = await _storage.readDeviceId();
    try {
      final response = await _refreshClient.post<Map<String, dynamic>>(
        ApiEndpoints.refreshToken,
        data: {
          ApiKeys.refreshToken: refreshToken,
          if (deviceId != null && deviceId.isNotEmpty)
            ApiKeys.deviceId: deviceId,
        },
      );

      final data = response.data;
      if (data == null || !await persistAuthResponse(data)) {
        return null;
      }

      return _readTokens(data)?.accessToken;
    } on DioException catch (error) {
      final code = _extractErrorCode(error);
      if (error.response?.statusCode == 401 || isTerminalAuthCode(code)) {
        await clearSession(notify: true);
      }
      return null;
    } catch (error) {
      debugPrint('Token refresh failed: $error');
      return null;
    }
  }

  SessionTokens? _readTokens(Map<String, dynamic> response) {
    final rawAccessToken = _readResponseValue(response, ApiKeys.accessToken)
        ?.toString();
    final refreshToken = _readResponseValue(response, ApiKeys.refreshToken)
        ?.toString();
    final accessToken = rawAccessToken == null
        ? null
        : AuthHeader.normalizeAccessToken(rawAccessToken);
    if (accessToken == null ||
        !AuthHeader.isCompactJwt(accessToken) ||
        refreshToken == null ||
        refreshToken.isEmpty) {
      return null;
    }

    return SessionTokens(
      accessToken: accessToken,
      refreshToken: refreshToken,
      sessionMode:
          _readResponseValue(response, ApiKeys.sessionMode)?.toString() ??
              SessionModes.normal,
      reloginRequiredAfterTrip:
          _readResponseValue(response, ApiKeys.reloginRequiredAfterTrip) ==
              true,
      continuationTripId: _parseInt(
        _readResponseValue(response, ApiKeys.continuationTripId),
      ),
      continuationAbsoluteExpiresAt: _parseDate(
        _readResponseValue(response, ApiKeys.continuationAbsoluteExpiresAt),
      ),
    );
  }

  Future<void> _persistTokens(SessionTokens tokens) async {
    await _storage.saveTokens(
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
    );
    await _storage.saveSessionMetadata(
      sessionMode: tokens.sessionMode,
      reloginRequired: tokens.reloginRequiredAfterTrip,
      continuationTripId: tokens.continuationTripId,
      continuationAbsoluteExpiresAt: tokens.continuationAbsoluteExpiresAt,
    );
  }

  bool _expiresSoon(String accessToken) {
    final expiresAt = _readExpiry(accessToken);
    if (expiresAt == null) {
      return false;
    }

    return expiresAt.isBefore(DateTime.now().toUtc().add(_refreshWindow));
  }

  DateTime? _readExpiry(String accessToken) {
    try {
      final payload = accessToken.split('.')[1];
      final normalized = base64Url.normalize(payload);
      final decoded = utf8.decode(base64Url.decode(normalized));
      final json = jsonDecode(decoded);
      if (json is! Map<String, dynamic>) return null;
      final exp = json['exp'];
      if (exp is! num) return null;
      return DateTime.fromMillisecondsSinceEpoch(
        exp.toInt() * 1000,
        isUtc: true,
      );
    } catch (_) {
      return null;
    }
  }

  Object? _readResponseValue(Map<String, dynamic> response, String key) {
    if (response.containsKey(key)) {
      return response[key];
    }

    final pascalKey = key[0].toUpperCase() + key.substring(1);
    return response[pascalKey];
  }

  String? _extractErrorCode(DioException error) {
    final data = error.response?.data;
    if (data is Map && data[ApiKeys.code] != null) {
      return data[ApiKeys.code].toString();
    }
    return null;
  }

  int? _parseInt(Object? value) {
    return int.tryParse(value?.toString() ?? '');
  }

  DateTime? _parseDate(Object? value) {
    return value == null ? null : DateTime.tryParse(value.toString())?.toUtc();
  }
}

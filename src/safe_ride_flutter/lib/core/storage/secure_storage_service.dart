import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../constants/app_strings.dart';

class SecureStorageService {
  final FlutterSecureStorage _storage;

  SecureStorageService({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  Future<void> saveTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    await Future.wait([
      _storage.write(key: StorageKeys.accessToken, value: accessToken),
      _storage.write(key: StorageKeys.refreshToken, value: refreshToken),
    ]);
  }

  Future<void> saveSessionMetadata({
    required String sessionMode,
    required bool reloginRequired,
    int? continuationTripId,
    DateTime? continuationAbsoluteExpiresAt,
  }) async {
    await Future.wait([
      _storage.write(key: StorageKeys.sessionMode, value: sessionMode),
      _storage.write(
        key: StorageKeys.reloginRequired,
        value: reloginRequired.toString(),
      ),
      if (continuationTripId == null)
        _storage.delete(key: StorageKeys.continuationTripId)
      else
        _storage.write(
          key: StorageKeys.continuationTripId,
          value: continuationTripId.toString(),
        ),
      if (continuationAbsoluteExpiresAt == null)
        _storage.delete(key: StorageKeys.continuationAbsoluteExpiresAt)
      else
        _storage.write(
          key: StorageKeys.continuationAbsoluteExpiresAt,
          value: continuationAbsoluteExpiresAt.toUtc().toIso8601String(),
        ),
    ]);
  }

  Future<String?> readSessionMode() {
    return _storage.read(key: StorageKeys.sessionMode);
  }

  Future<bool> readReloginRequired() async {
    return (await _storage.read(key: StorageKeys.reloginRequired)) == 'true';
  }

  Future<int?> readContinuationTripId() async {
    final raw = await _storage.read(key: StorageKeys.continuationTripId);
    return int.tryParse(raw ?? '');
  }

  Future<DateTime?> readContinuationAbsoluteExpiresAt() async {
    final raw = await _storage.read(
      key: StorageKeys.continuationAbsoluteExpiresAt,
    );
    return raw == null ? null : DateTime.tryParse(raw)?.toUtc();
  }

  Future<void> saveUserProfile(String profileJson) {
    return _storage.write(key: StorageKeys.userProfile, value: profileJson);
  }

  Future<String?> readUserProfile() {
    return _storage.read(key: StorageKeys.userProfile);
  }

  Future<String?> readRefreshToken() {
    return _storage.read(key: StorageKeys.refreshToken);
  }

  Future<String?> readAccessToken() {
    return _storage.read(key: StorageKeys.accessToken);
  }

  // Future<String?> readAccessToken() {
  //   return _storage.read(key: _accessTokenKey);
  // }

  Future<String?> readDeviceId() {
    return _storage.read(key: StorageKeys.deviceId);
  }

  Future<void> saveDeviceId(String deviceId) {
    return _storage.write(key: StorageKeys.deviceId, value: deviceId);
  }

  Future<void> savePendingTripShareToken(String token) {
    return _storage.write(key: StorageKeys.pendingTripShareToken, value: token);
  }

  Future<String?> readPendingTripShareToken() {
    return _storage.read(key: StorageKeys.pendingTripShareToken);
  }

  Future<void> deletePendingTripShareToken() {
    return _storage.delete(key: StorageKeys.pendingTripShareToken);
  }

  Future<void> clearTokens() async {
    await Future.wait([
      _storage.delete(key: StorageKeys.accessToken),
      _storage.delete(key: StorageKeys.refreshToken),
      _storage.delete(key: StorageKeys.userProfile),
      _storage.delete(key: StorageKeys.sessionMode),
      _storage.delete(key: StorageKeys.reloginRequired),
      _storage.delete(key: StorageKeys.continuationTripId),
      _storage.delete(key: StorageKeys.continuationAbsoluteExpiresAt),
    ]);
  }
}

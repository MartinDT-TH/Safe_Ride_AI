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

  Future<void> clearTokens() async {
    await Future.wait([
      _storage.delete(key: StorageKeys.accessToken),
      _storage.delete(key: StorageKeys.refreshToken),
    ]);
  }
}


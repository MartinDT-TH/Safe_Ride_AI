import 'dart:math';

import 'package:flutter/foundation.dart';

import '../constants/app_strings.dart';
import '../storage/secure_storage_service.dart';

class DeviceIdentity {
  final String id;
  final String name;

  const DeviceIdentity({required this.id, required this.name});
}

class DeviceIdentityService {
  final SecureStorageService _storage;

  DeviceIdentityService(this._storage);

  Future<DeviceIdentity> getIdentity() async {
    var deviceId = await _storage.readDeviceId();
    if (deviceId == null || deviceId.isEmpty) {
      deviceId = _generateDeviceId();
      await _storage.saveDeviceId(deviceId);
    }

    return DeviceIdentity(id: deviceId, name: _deviceName);
  }

  String get _deviceName {
    return switch (defaultTargetPlatform) {
      TargetPlatform.android => DeviceStrings.android,
      TargetPlatform.iOS => DeviceStrings.ios,
      TargetPlatform.macOS => DeviceStrings.macos,
      TargetPlatform.windows => DeviceStrings.windows,
      TargetPlatform.linux => DeviceStrings.linux,
      TargetPlatform.fuchsia => DeviceStrings.fuchsia,
    };
  }

  String _generateDeviceId() {
    final random = Random.secure();
    final randomPart = List.generate(
      16,
      (_) => random.nextInt(256).toRadixString(16).padLeft(2, '0'),
    ).join();
    return '${DeviceStrings.idPrefix}-'
        '${DateTime.now().microsecondsSinceEpoch}-$randomPart';
  }
}


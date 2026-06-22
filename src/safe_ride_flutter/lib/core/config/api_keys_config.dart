import 'package:flutter/foundation.dart';

abstract final class ApiKeysConfig {
  static const googleMaps = String.fromEnvironment('GOOGLE_MAPS_API_KEY');
  static const vietMap = String.fromEnvironment('VIETMAP_API_KEY');

  static const googleServerClientId = String.fromEnvironment(
    'GOOGLE_SERVER_CLIENT_ID',
  );
  static const firebaseWeb = String.fromEnvironment('FIREBASE_WEB_API_KEY');
  static const sentryDsn = String.fromEnvironment('SENTRY_DSN');

  static bool get hasNativeAndroidConfig =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

  static bool get hasGoogleMapsKey =>
      googleMaps.trim().isNotEmpty || hasNativeAndroidConfig;
  static bool get hasGoogleServerClientId =>
      googleServerClientId.trim().isNotEmpty || hasNativeAndroidConfig;
  static bool get hasVietMapKey => vietMap.trim().isNotEmpty;
}


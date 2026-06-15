abstract final class ApiKeysConfig {
  static const googleMaps = String.fromEnvironment('GOOGLE_MAPS_API_KEY');
  static const googleServerClientId = String.fromEnvironment(
    'GOOGLE_SERVER_CLIENT_ID',
  );
  static const firebaseWeb = String.fromEnvironment('FIREBASE_WEB_API_KEY');
  static const sentryDsn = String.fromEnvironment('SENTRY_DSN');

  static bool get hasGoogleMapsKey => googleMaps.trim().isNotEmpty;
  static bool get hasGoogleServerClientId =>
      googleServerClientId.trim().isNotEmpty;
}

# Safe Ride

## Local configuration

Create `env/api_keys.local.json` from `env/api_keys.example.json`, then run:

```powershell
flutter run --dart-define-from-file=env/api_keys.local.json
```

VS Code and Android Studio users can select the `Safe Ride (local)` run
configuration. Stop the existing app process before changing configuration;
hot reload and hot restart cannot add compile-time Dart defines.

`GOOGLE_SERVER_CLIENT_ID` must be a Web OAuth client ID. Google Cloud must
also contain an Android OAuth client configured with:

- Package name: `com.android.safe_ride`
- SHA-1 fingerprint of the key used to sign the app

To print the debug SHA-1:

```powershell
keytool -list -v -alias androiddebugkey `
  -keystore "$env:USERPROFILE\.android\debug.keystore" `
  -storepass android -keypass android
```

## Google Maps Android

Use a dedicated Android API key for `GOOGLE_MAPS_API_KEY`. In Google Cloud:

1. Enable `Maps SDK for Android`.
2. Enable billing for the project.
3. Restrict the key to Android apps.
4. Add package name `com.android.safe_ride`.
5. Add the SHA-1 fingerprint of the signing key.

Do not reuse a server-restricted Routes or Geocoding key for the Android map.

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/session/session_manager.dart';
import 'package:safe_ride/core/storage/secure_storage_service.dart';
import 'package:safe_ride/features/trip_sharing/data/datasources/trip_sharing_remote_datasource.dart';
import 'package:safe_ride/features/trip_sharing/trip_share_deep_link_coordinator.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late SecureStorageService storage;
  late TripShareDeepLinkCoordinator coordinator;

  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
    storage = SecureStorageService();
    coordinator = TripShareDeepLinkCoordinator(
      storage,
      SessionManager(
        storage: storage,
        refreshClient: Dio(BaseOptions(baseUrl: 'http://localhost')),
      ),
      TripSharingRemoteDatasource(dio: Dio()),
      () => 'https://app.saferide.vn',
    );
  });

  tearDown(() => coordinator.dispose());

  test(
    'captures a valid share token before login for later continuation',
    () async {
      await coordinator.handleUri(
        Uri.parse('https://app.saferide.vn/trip-share?t=raw-token-value'),
      );

      expect(await storage.readPendingTripShareToken(), 'raw-token-value');
    },
  );

  test('ignores links outside the configured app-link boundary', () async {
    await coordinator.handleUri(
      Uri.parse('https://example.com/trip-share?t=untrusted-token'),
    );

    expect(await storage.readPendingTripShareToken(), isNull);
  });

  test('matches an explicitly configured development custom scheme', () {
    expect(
      TripShareDeepLinkCoordinator.matchesConfiguredAppLink(
        Uri.parse('saferide-dev://share/trip-share?t=token'),
        'saferide-dev://share',
      ),
      isTrue,
    );
  });
}

import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/constants/app_strings.dart';
import 'package:safe_ride/core/network/auth_token_refresh_interceptor.dart';
import 'package:safe_ride/core/session/session_manager.dart';
import 'package:safe_ride/core/storage/secure_storage_service.dart';
import 'package:safe_ride/features/trip_sharing/data/datasources/trip_sharing_remote_datasource.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('create attaches the current SessionManager access token', () async {
    final accessToken = _jwt(
      expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
    );
    FlutterSecureStorage.setMockInitialValues({
      StorageKeys.accessToken: accessToken,
      StorageKeys.refreshToken: 'refresh-token',
    });
    final storage = SecureStorageService();
    final sessionManager = SessionManager(storage: storage);
    final requestAdapter = _RecordingAdapter((_) => _createdShareResponse());
    final dio = Dio(BaseOptions(baseUrl: 'https://example.test/api/'))
      ..httpClientAdapter = requestAdapter;
    dio.options.headers['authorization'] = 'Bearer stale.jwt.signature';
    dio.interceptors.add(
      AuthTokenRefreshInterceptor(
        retryClient: Dio(BaseOptions(baseUrl: 'https://example.test/api/')),
        sessionManager: sessionManager,
      ),
    );

    await TripSharingRemoteDatasource(
      dio: dio,
    ).create(tripId: 12, recipientPhoneNumber: '+84901234567');

    expect(requestAdapter.requests, hasLength(1));
    expect(requestAdapter.requests.single.path, '/trips/12/shares');
    expect(
      requestAdapter.requests.single.uri.toString(),
      'https://example.test/api/trips/12/shares',
    );
    expect(
      requestAdapter.requests.single.headers[ApiKeys.authorization],
      'Bearer $accessToken',
    );
    expect(
      requestAdapter.requests.single.headers.keys.where(
        (key) => key.toLowerCase() == ApiKeys.authorization.toLowerCase(),
      ),
      hasLength(1),
    );
  });

  test('create refreshes after 401 and retries exactly once', () async {
    final initialToken = _jwt(
      expiresAt: DateTime.now().toUtc().add(const Duration(hours: 1)),
      marker: 'initial',
    );
    final refreshedToken = _jwt(
      expiresAt: DateTime.now().toUtc().add(const Duration(hours: 2)),
      marker: 'refreshed',
    );
    FlutterSecureStorage.setMockInitialValues({
      StorageKeys.accessToken: initialToken,
      StorageKeys.refreshToken: 'refresh-token',
    });

    final refreshAdapter = _RecordingAdapter(
      (_) => ResponseBody.fromString(
        jsonEncode({
          ApiKeys.accessToken: refreshedToken,
          ApiKeys.refreshToken: 'rotated-refresh-token',
        }),
        200,
        headers: _jsonHeaders,
      ),
    );
    final refreshDio = Dio(BaseOptions(baseUrl: 'https://example.test/api/'))
      ..httpClientAdapter = refreshAdapter;
    final sessionManager = SessionManager(
      storage: SecureStorageService(),
      refreshClient: refreshDio,
    );

    final initialAdapter = _RecordingAdapter(
      (_) => ResponseBody.fromString(
        jsonEncode({
          ApiKeys.code: 'auth.access_token_invalid',
          'detail': 'Access token is invalid.',
          'traceId': 'trace-initial-401',
        }),
        401,
        headers: _problemHeaders,
      ),
    );
    final retryAdapter = _RecordingAdapter((_) => _createdShareResponse());
    final retryDio = Dio(BaseOptions(baseUrl: 'https://example.test/api/'))
      ..httpClientAdapter = retryAdapter;
    final dio = Dio(BaseOptions(baseUrl: 'https://example.test/api/'))
      ..httpClientAdapter = initialAdapter;
    dio.interceptors.add(
      AuthTokenRefreshInterceptor(
        retryClient: retryDio,
        sessionManager: sessionManager,
      ),
    );

    final created = await TripSharingRemoteDatasource(
      dio: dio,
    ).create(tripId: 12, recipientPhoneNumber: '+84901234567');

    expect(created.tripShareId, 42);
    expect(initialAdapter.requests, hasLength(1));
    expect(
      initialAdapter.requests.single.headers[ApiKeys.authorization],
      'Bearer $initialToken',
    );
    expect(refreshAdapter.requests, hasLength(1));
    expect(refreshAdapter.requests.single.path, ApiEndpoints.refreshToken);
    expect(retryAdapter.requests, hasLength(1));
    expect(
      retryAdapter.requests.single.headers[ApiKeys.authorization],
      'Bearer $refreshedToken',
    );
    expect(retryAdapter.requests.single.extra['auth_refresh_retried'], isTrue);
  });
}

const _jsonHeaders = <String, List<String>>{
  Headers.contentTypeHeader: [Headers.jsonContentType],
};

const _problemHeaders = <String, List<String>>{
  Headers.contentTypeHeader: ['application/problem+json'],
};

ResponseBody _createdShareResponse() => ResponseBody.fromString(
  jsonEncode({
    'tripShareId': 42,
    'recipient': {
      'userId': 'recipient-id',
      'fullName': 'Người nhận',
      'maskedPhoneNumber': '0901***567',
    },
    'shareUrl': 'https://app.saferide.vn/trip-share?t=test',
    'expiresAt': '2026-07-22T00:00:00Z',
  }),
  200,
  headers: _jsonHeaders,
);

String _jwt({required DateTime expiresAt, String marker = 'current'}) {
  final header = base64Url.encode(utf8.encode(jsonEncode({'alg': 'HS256'})));
  final payload = base64Url.encode(
    utf8.encode(
      jsonEncode({
        'exp': expiresAt.millisecondsSinceEpoch ~/ 1000,
        'marker': marker,
        'role': 'Customer',
      }),
    ),
  );
  return '$header.$payload.test-signature-$marker';
}

class _RecordingAdapter implements HttpClientAdapter {
  _RecordingAdapter(this._respond);

  final ResponseBody Function(RequestOptions options) _respond;
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    return _respond(options);
  }

  @override
  void close({bool force = false}) {}
}

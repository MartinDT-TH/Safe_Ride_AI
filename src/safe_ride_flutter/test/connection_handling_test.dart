import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:safe_ride/core/network/dio_client.dart';
import 'package:safe_ride/core/services/connectivity_service.dart';

void main() {
  group('ConnectivityService', () {
    test('offline has priority over server failures', () async {
      final network = _FakeNetworkConnectivity([ConnectivityResult.none]);
      final service = ConnectivityService(connectivity: network);

      await service.refreshNetworkStatus();
      service.reportServerUnavailable();

      expect(service.status, ConnectionStatus.offline);
      service.dispose();
      await network.dispose();
    });

    test(
      'network availability does not clear a known server failure',
      () async {
        final network = _FakeNetworkConnectivity([ConnectivityResult.wifi]);
        final service = ConnectivityService(connectivity: network);

        await service.refreshNetworkStatus();
        service.reportServerUnavailable();
        await service.refreshNetworkStatus();

        expect(service.status, ConnectionStatus.serverUnavailable);

        service.reportServerReachable();
        expect(service.status, ConnectionStatus.online);
        service.dispose();
        await network.dispose();
      },
    );

    test(
      'realtime loss starts recovery and reloads after retry succeeds',
      () async {
        final network = _FakeNetworkConnectivity([ConnectivityResult.wifi]);
        final server = _FakeServerReachability([true]);
        final service = ConnectivityService(
          connectivity: network,
          serverReachability: server,
        );
        await service.refreshNetworkStatus();
        var reloadRequests = 0;
        final subscription = service.reloadRequests.listen(
          (_) => reloadRequests++,
        );

        await service.handleRealtimeConnectionLost();
        expect(service.status, ConnectionStatus.serverUnavailable);

        final recovered = await service.retryServerConnection();
        await Future<void>.delayed(Duration.zero);

        expect(recovered, isTrue);
        expect(server.checkCount, 1);
        expect(service.status, ConnectionStatus.online);
        expect(reloadRequests, 1);
        await subscription.cancel();
        service.dispose();
        await network.dispose();
      },
    );

    test('offline retry automatically reloads when internet returns', () async {
      final network = _FakeNetworkConnectivity([ConnectivityResult.none]);
      final service = ConnectivityService(
        connectivity: network,
        networkRetryInterval: const Duration(milliseconds: 10),
      );
      var reloadRequests = 0;
      final subscription = service.reloadRequests.listen(
        (_) => reloadRequests++,
      );

      await service.refreshNetworkStatus();
      expect(service.status, ConnectionStatus.offline);

      network.results = [ConnectivityResult.wifi];
      await Future<void>.delayed(const Duration(milliseconds: 30));

      expect(service.status, ConnectionStatus.online);
      expect(reloadRequests, 1);
      await subscription.cancel();
      service.dispose();
      await network.dispose();
    });
  });

  group('DioErrorInterceptor', () {
    test('connection error while offline remains offline', () async {
      final network = _FakeNetworkConnectivity([ConnectivityResult.none]);
      final service = ConnectivityService(connectivity: network);
      final interceptor = DioErrorInterceptor(connectivityService: service);

      await interceptor.handleError(
        DioException(
          requestOptions: RequestOptions(path: '/bookings'),
          type: DioExceptionType.connectionError,
        ),
      );

      expect(service.status, ConnectionStatus.offline);
      service.dispose();
      await network.dispose();
    });

    test('repeated 500 responses create one persistent server state', () async {
      final network = _FakeNetworkConnectivity([ConnectivityResult.wifi]);
      final service = ConnectivityService(connectivity: network);
      final interceptor = DioErrorInterceptor(connectivityService: service);
      await service.refreshNetworkStatus();
      var notifications = 0;
      service.addListener(() => notifications++);
      final request = RequestOptions(path: '/bookings');
      final error = DioException.badResponse(
        statusCode: 500,
        requestOptions: request,
        response: Response<void>(requestOptions: request, statusCode: 500),
      );

      await interceptor.handleError(error);
      await interceptor.handleError(error);

      expect(service.status, ConnectionStatus.serverUnavailable);
      expect(notifications, 1);
      service.dispose();
      await network.dispose();
    });

    test('4xx response proves that the server is reachable', () async {
      final network = _FakeNetworkConnectivity([ConnectivityResult.wifi]);
      final service = ConnectivityService(connectivity: network);
      final interceptor = DioErrorInterceptor(connectivityService: service);
      await service.refreshNetworkStatus();
      service.reportServerUnavailable();
      final request = RequestOptions(path: '/bookings');

      await interceptor.handleError(
        DioException.badResponse(
          statusCode: 401,
          requestOptions: request,
          response: Response<void>(requestOptions: request, statusCode: 401),
        ),
      );

      expect(service.status, ConnectionStatus.online);
      service.dispose();
      await network.dispose();
    });

    test(
      'silent background request does not show server unavailable',
      () async {
        final network = _FakeNetworkConnectivity([ConnectivityResult.wifi]);
        final service = ConnectivityService(connectivity: network);
        final interceptor = DioErrorInterceptor(connectivityService: service);
        await service.refreshNetworkStatus();
        final request = RequestOptions(
          path: '/trip-shares/received',
          extra: {DioRequestExtras.suppressGlobalErrorSnackBar: true},
        );

        await interceptor.handleError(
          DioException.badResponse(
            statusCode: 500,
            requestOptions: request,
            response: Response<void>(requestOptions: request, statusCode: 500),
          ),
        );

        expect(service.status, ConnectionStatus.online);
        service.dispose();
        await network.dispose();
      },
    );
  });
}

class _FakeNetworkConnectivity implements NetworkConnectivity {
  _FakeNetworkConnectivity(this.results);

  List<ConnectivityResult> results;
  final StreamController<List<ConnectivityResult>> _changes =
      StreamController<List<ConnectivityResult>>.broadcast();

  @override
  Future<List<ConnectivityResult>> checkConnectivity() async => results;

  @override
  Stream<List<ConnectivityResult>> get onConnectivityChanged => _changes.stream;

  Future<void> dispose() => _changes.close();
}

class _FakeServerReachability implements ServerReachability {
  _FakeServerReachability(this._results);

  final List<bool> _results;
  int checkCount = 0;

  @override
  Future<bool> check() async {
    final index = checkCount < _results.length
        ? checkCount
        : _results.length - 1;
    checkCount++;
    return _results[index];
  }
}

import 'package:signalr_netcore/signalr_client.dart';

import '../constants/app_strings.dart';

class DriverLocationUpdate {
  const DriverLocationUpdate({
    required this.driverId,
    required this.customerId,
    required this.tripId,
    required this.latitude,
    required this.longitude,
    required this.updatedAt,
  });

  final String driverId;
  final String customerId;
  final int tripId;
  final double latitude;
  final double longitude;
  final DateTime? updatedAt;

  static DriverLocationUpdate? fromSignalRArguments(List<Object?>? arguments) {
    if (arguments == null || arguments.isEmpty || arguments.first is! Map) {
      return null;
    }

    final data = Map<String, dynamic>.from(arguments.first as Map);
    final tripId = (_value(data, ApiKeys.tripId) as num?)?.toInt();
    final latitude = (_value(data, ApiKeys.latitude) as num?)?.toDouble();
    final longitude = (_value(data, ApiKeys.longitude) as num?)?.toDouble();
    if (tripId == null || latitude == null || longitude == null) {
      return null;
    }

    return DriverLocationUpdate(
      driverId: _value(data, ApiKeys.driverId)?.toString() ?? '',
      customerId: _value(data, 'customerId')?.toString() ?? '',
      tripId: tripId,
      latitude: latitude,
      longitude: longitude,
      updatedAt: _value(data, 'updatedAt') == null
          ? null
          : DateTime.tryParse(_value(data, 'updatedAt').toString()),
    );
  }

  static Object? _value(Map<String, dynamic> data, String key) {
    final pascalKey = key.isEmpty
        ? key
        : '${key[0].toUpperCase()}${key.substring(1)}';
    return data[key] ?? data[pascalKey];
  }
}

class SocketService {
  HubConnection? _connection;
  String? _accessToken;

  bool get isConnected => _connection?.state == HubConnectionState.Connected;

  Future<void> connect(String accessToken) async {
    if (accessToken.isEmpty) {
      return;
    }

    if (isConnected && _accessToken == accessToken) {
      return;
    }

    if (_connection != null && _accessToken != accessToken) {
      await disconnect();
    }

    _accessToken = accessToken;
    _connection ??= HubConnectionBuilder()
        .withUrl(
          _hubUrl,
          options: HttpConnectionOptions(
            accessTokenFactory: () async => accessToken,
            requestTimeout: 10000,
          ),
        )
        .withAutomaticReconnect()
        .build();

    if (_connection!.state == HubConnectionState.Disconnected) {
      await _connection!.start();
    }
  }

  void onDriverLocationUpdated(
    void Function(DriverLocationUpdate update) handler,
  ) {
    _connection?.off('DriverLocationUpdated');
    _connection?.on('DriverLocationUpdated', (arguments) {
      final update = DriverLocationUpdate.fromSignalRArguments(arguments);
      if (update != null) {
        handler(update);
      }
    });
  }

  Future<void> joinTrip(int tripId) async {
    await _connection?.invoke('JoinTrip', args: <Object>[tripId]);
  }

  Future<void> leaveTrip(int tripId) async {
    await _connection?.invoke('LeaveTrip', args: <Object>[tripId]);
  }

  Future<void> disconnect() async {
    await _connection?.stop();
    _connection = null;
    _accessToken = null;
  }

  static String get _hubUrl {
    final apiBase = AppConfig.apiBaseUrl.endsWith('/')
        ? AppConfig.apiBaseUrl.substring(0, AppConfig.apiBaseUrl.length - 1)
        : AppConfig.apiBaseUrl;
    final root = apiBase.endsWith('/api')
        ? apiBase.substring(0, apiBase.length - 4)
        : apiBase;
    return '$root/hubs/saferide';
  }
}

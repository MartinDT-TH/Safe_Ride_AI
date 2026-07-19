import 'package:flutter/foundation.dart';
import 'package:signalr_netcore/signalr_client.dart';
import 'package:safe_ride/core/constants/app_strings.dart';

class TripChatSocketService {
  HubConnection? _connection;

  bool get isConnected => _connection?.state == HubConnectionState.Connected;

  Future<void> connect(String token, {
    required Function(List<Object?>?) onMessageReceived,
  }) async {
    if (_connection?.state == HubConnectionState.Connected) return;

    final hubUrl = _buildHubUrl();

    final options = HttpConnectionOptions(
      accessTokenFactory: () async => token,
      transport: HttpTransportType.WebSockets,
      skipNegotiation: true,
    );

    _connection = HubConnectionBuilder()
        .withUrl(hubUrl, options: options)
        .withAutomaticReconnect()
        .build();

    _connection!.on('TripMessageReceived', onMessageReceived);

    try {
      debugPrint('CHAT_SOCKET: Connecting to $hubUrl');
      await _connection!.start();
      debugPrint('CHAT_SOCKET: Connected');
    } catch (e) {
      debugPrint('CHAT_SOCKET: Connection failed: $e');
      rethrow;
    }
  }

  Future<void> joinTripChat(int tripId) async {
    await _invoke('JoinTripChat', [tripId]);
  }

  Future<void> leaveTripChat(int tripId) async {
    await _invoke('LeaveTripChat', [tripId]);
  }

  Future<void> sendTripMessage(int tripId, String message) async {
    await _invoke('SendTripMessage', [tripId, message]);
  }

  Future<void> _invoke(String method, List<Object> args) async {
    if (_connection?.state != HubConnectionState.Connected) {
      debugPrint('CHAT_SOCKET: Cannot invoke $method - Not connected');
      return;
    }
    try {
      await _connection!.invoke(method, args: args);
    } catch (e) {
      debugPrint('CHAT_SOCKET: Invoke $method failed: $e');
    }
  }

  Future<void> disconnect() async {
    if (_connection != null) {
      await _connection!.stop();
      _connection = null;
    }
  }

  String _buildHubUrl() {
    final apiBase = AppConfig.apiBaseUrl.endsWith('/')
        ? AppConfig.apiBaseUrl.substring(0, AppConfig.apiBaseUrl.length - 1)
        : AppConfig.apiBaseUrl;
    final root = apiBase.endsWith('/api')
        ? apiBase.substring(0, apiBase.length - 4)
        : apiBase;

    var url = '$root/hubs/trip-chat';
    if (url.startsWith('https://')) {
      url = url.replaceFirst('https://', 'wss://');
    } else if (url.startsWith('http://')) {
      url = url.replaceFirst('http://', 'ws://');
    }
    return url;
  }
}

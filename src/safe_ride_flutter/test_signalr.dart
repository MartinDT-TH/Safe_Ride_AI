import 'package:signalr_netcore/signalr_client.dart';
void main() async {
  final url = 'http://localhost:5026/hubs/saferide';
  print('Connecting to $url');
  final options = HttpConnectionOptions(skipNegotiation: true, transport: HttpTransportType.WebSockets, requestTimeout: 10000);
  final connection = HubConnectionBuilder().withUrl(url, options: options).build();
  try {
    await connection.start();
    print('Connected successfully!');
    await connection.stop();
  } catch (e, stackTrace) {
    print('Connection failed: $e');
    print(stackTrace);
  }
}

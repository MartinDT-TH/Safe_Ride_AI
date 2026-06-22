import 'package:dio/dio.dart';
void main() {
  final dio = Dio(BaseOptions(baseUrl: 'http://10.0.2.2:5026/api/'));
  try { dio.get('/api/maps/reverse'); } on DioException catch (e) { print('1. /api/maps/reverse => ' + e.requestOptions.uri.toString()); }
  try { dio.get('api/maps/reverse'); } on DioException catch (e) { print('2. api/maps/reverse => ' + e.requestOptions.uri.toString()); }
  try { dio.get('/maps/reverse'); } on DioException catch (e) { print('3. /maps/reverse => ' + e.requestOptions.uri.toString()); }
  try { dio.get('maps/reverse'); } on DioException catch (e) { print('4. maps/reverse => ' + e.requestOptions.uri.toString()); }
}

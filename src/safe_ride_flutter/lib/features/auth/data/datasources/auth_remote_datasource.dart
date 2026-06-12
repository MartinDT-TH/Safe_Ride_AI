// import 'package:dio/dio.dart';
//
// class AuthRemoteDatasource {
//   final Dio dio;
//
//   AuthRemoteDatasource(this.dio);
//
//   Future<void> login(String phone) async {
//     await dio.post(
//       '/auth/login',
//       data: {
//         'phone': phone,
//       },
//     );
//   }
// }

import 'package:dio/dio.dart';

import '../../../../core/network/dio_client.dart';

class AuthRemoteDatasource {
  final Dio _dio;

  AuthRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  Future<Map<String, dynamic>> login(String phone) async {
    final response = await _dio.post(
      '/auth/send-otp',
      data: {'phoneNumber': phone},
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> verifyOtp(
    String phone,
    String otpCode,
    String deviceId,
    String deviceName,
  ) async {
    final response = await _dio.post(
      '/auth/verify-otp',
      data: {
        'phoneNumber': phone,
        'otpCode': otpCode,
        'deviceId': deviceId,
        'deviceName': deviceName,
      },
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> googleLogin(
    String googleIdToken,
    String deviceId,
    String deviceName,
  ) async {
    final response = await _dio.post(
      '/auth/google-login',
      data: {
        'googleIdToken': googleIdToken,
        'deviceId': deviceId,
        'deviceName': deviceName,
      },
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<void> logout(String refreshToken) async {
    await _dio.post('/auth/logout', data: {'refreshToken': refreshToken});
  }
}

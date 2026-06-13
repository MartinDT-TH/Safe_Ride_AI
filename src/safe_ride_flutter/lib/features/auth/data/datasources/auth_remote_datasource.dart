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
import 'package:http_parser/http_parser.dart';

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

  Future<Map<String, dynamic>> updateProfile(
    String accessToken,
    String fullName,
    String? email,
  ) async {
    final response = await _dio.put(
      '/auth/profile',
      data: {
        'fullName': fullName,
        'email': email?.trim().isEmpty == true ? null : email?.trim(),
      },
      options: Options(headers: {'Authorization': 'Bearer $accessToken'}),
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<String> uploadAvatar(String accessToken, String filePath) async {
    final fileName = filePath.split(RegExp(r'[/\\]')).last;
    final extension = fileName.split('.').last.toLowerCase();
    final contentType = switch (extension) {
      'png' => 'image/png',
      'webp' => 'image/webp',
      _ => 'image/jpeg',
    };
    final response = await _dio.post(
      '/auth/profile/avatar',
      data: FormData.fromMap({
        'file': await MultipartFile.fromFile(
          filePath,
          filename: fileName,
          contentType: MediaType.parse(contentType),
        ),
      }),
      options: Options(
        headers: {'Authorization': 'Bearer $accessToken'},
        contentType: 'multipart/form-data',
      ),
    );

    final data = Map<String, dynamic>.from(response.data as Map);
    return data['avatarUrl']?.toString() ?? '';
  }

  Future<void> logout(String refreshToken) async {
    await _dio.post('/auth/logout', data: {'refreshToken': refreshToken});
  }
}

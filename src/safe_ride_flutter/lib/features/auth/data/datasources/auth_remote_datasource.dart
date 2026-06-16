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

import '../../../../core/constants/app_strings.dart';
import '../../../../core/network/auth_header.dart';
import '../../../../core/network/dio_client.dart';

class AuthRemoteDatasource {
  final Dio _dio;

  AuthRemoteDatasource({Dio? dio}) : _dio = dio ?? DioClient().dio;

  Future<Map<String, dynamic>> login(String phone) async {
    final response = await _dio.post(
      ApiEndpoints.sendOtp,
      data: {ApiKeys.phoneNumber: phone},
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
      ApiEndpoints.verifyOtp,
      data: {
        ApiKeys.phoneNumber: phone,
        ApiKeys.otpCode: otpCode,
        ApiKeys.deviceId: deviceId,
        ApiKeys.deviceName: deviceName,
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
      ApiEndpoints.googleLogin,
      data: {
        ApiKeys.googleIdToken: googleIdToken,
        ApiKeys.deviceId: deviceId,
        ApiKeys.deviceName: deviceName,
      },
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> updateProfile(
    String accessToken,
    String fullName,
    String? phoneNumber,
    String? email,
  ) async {
    final response = await _dio.put(
      ApiEndpoints.profile,
      data: {
        ApiKeys.fullName: fullName,
        ApiKeys.phoneNumber: phoneNumber?.trim().isEmpty == true
            ? null
            : phoneNumber?.trim(),
        ApiKeys.email: email?.trim().isEmpty == true ? null : email?.trim(),
      },
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
      ),
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> sendProfilePhoneOtp(
    String accessToken,
    String phoneNumber,
  ) async {
    final response = await _dio.post(
      ApiEndpoints.profilePhoneSendOtp,
      data: {ApiKeys.phoneNumber: phoneNumber},
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
      ),
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> verifyProfilePhoneOtp(
    String accessToken,
    String phoneNumber,
    String otpCode,
  ) async {
    final response = await _dio.post(
      ApiEndpoints.profilePhoneVerifyOtp,
      data: {ApiKeys.phoneNumber: phoneNumber, ApiKeys.otpCode: otpCode},
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
      ),
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> getLinkedAccounts(String accessToken) async {
    final response = await _dio.get(
      ApiEndpoints.linkedAccounts,
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
      ),
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> linkGoogleAccount(
    String accessToken,
    String googleIdToken,
  ) async {
    final response = await _dio.post(
      ApiEndpoints.linkedGoogleAccount,
      data: {ApiKeys.googleIdToken: googleIdToken},
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
      ),
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<Map<String, dynamic>> unlinkGoogleAccount(String accessToken) async {
    final response = await _dio.delete(
      ApiEndpoints.linkedGoogleAccount,
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
      ),
    );

    return Map<String, dynamic>.from(response.data as Map);
  }

  Future<String> uploadAvatar(String accessToken, String filePath) async {
    final fileName = filePath.split(RegExp(r'[/\\]')).last;
    final extension = fileName.split('.').last.toLowerCase();
    final contentType = switch (extension) {
      AppValues.pngExtension => AppValues.pngMimeType,
      AppValues.webpExtension => AppValues.webpMimeType,
      _ => AppValues.jpegMimeType,
    };
    final response = await _dio.post(
      ApiEndpoints.profileAvatar,
      data: FormData.fromMap({
        ApiKeys.file: await MultipartFile.fromFile(
          filePath,
          filename: fileName,
          contentType: MediaType.parse(contentType),
        ),
      }),
      options: Options(
        headers: {ApiKeys.authorization: AuthHeader.bearer(accessToken)},
        contentType: AppValues.multipartFormData,
      ),
    );

    final data = Map<String, dynamic>.from(response.data as Map);
    return data[ApiKeys.avatarUrl]?.toString() ?? '';
  }

  Future<void> logout(String refreshToken) async {
    await _dio.post(
      ApiEndpoints.logout,
      data: {ApiKeys.refreshToken: refreshToken},
    );
  }
}

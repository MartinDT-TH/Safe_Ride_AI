// import '../../domain/repositories/auth_repository.dart';
// import '../datasources/auth_remote_datasource.dart';
//
// class AuthRepositoryImpl implements AuthRepository {
//   final AuthRemoteDatasource remoteDatasource;
//
//   AuthRepositoryImpl(this.remoteDatasource);
//
//   @override
//   Future<void> login(String phone) async {
//     await remoteDatasource.login(phone);
//   }
// }

import '../../domain/repositories/auth_repository.dart';

import '../datasources/auth_remote_datasource.dart';

class AuthRepositoryImpl implements AuthRepository {
  final AuthRemoteDatasource remoteDatasource;

  AuthRepositoryImpl(this.remoteDatasource);

  @override
  Future<Map<String, dynamic>> login(String phone) async {
    return await remoteDatasource.login(phone);
  }

  @override
  Future<Map<String, dynamic>> verifyOtp(
    String phone,
    String otpCode,
    String deviceId,
    String deviceName,
  ) async {
    return await remoteDatasource.verifyOtp(
      phone,
      otpCode,
      deviceId,
      deviceName,
    );
  }

  @override
  Future<Map<String, dynamic>> googleLogin(
    String googleIdToken,
    String deviceId,
    String deviceName,
  ) async {
    return await remoteDatasource.googleLogin(
      googleIdToken,
      deviceId,
      deviceName,
    );
  }

  @override
  Future<void> logout(String refreshToken) {
    return remoteDatasource.logout(refreshToken);
  }
}

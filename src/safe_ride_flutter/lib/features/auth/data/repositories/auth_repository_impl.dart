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
  Future<Map<String, dynamic>> verifyOtp(String phone, String otpCode) async {
    return await remoteDatasource.verifyOtp(phone, otpCode);
  }

  @override
  Future<Map<String, dynamic>> googleLogin(String googleIdToken) async {
    return await remoteDatasource.googleLogin(googleIdToken);
  }
}

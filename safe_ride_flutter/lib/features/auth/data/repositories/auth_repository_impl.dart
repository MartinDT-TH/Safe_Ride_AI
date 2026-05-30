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
  Future<Map<String, dynamic>> login(
      String phone,
      ) async {

    return await remoteDatasource.login(phone);
  }
}
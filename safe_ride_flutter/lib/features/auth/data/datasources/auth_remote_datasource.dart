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

import 'dart:async';

class AuthRemoteDatasource {

  Future<Map<String, dynamic>> login(
      String phone,
      ) async {

    await Future.delayed(
      const Duration(seconds: 2),
    );

    return {
      'success': true,

      'message': 'Login success',

      'data': {
        'id': 1,
        'name': 'Alex',
        'phone': phone,
        'token': 'fake_jwt_token',
      }
    };
  }
}
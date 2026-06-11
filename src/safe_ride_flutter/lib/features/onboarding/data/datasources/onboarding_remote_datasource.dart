import 'package:dio/dio.dart';

// class OnboardingRemoteDatasource {
//   final Dio dio;
//
//   OnboardingRemoteDatasource(this.dio);
//
//   Future<void> selectRole(String role) async {
//     await dio.post(
//       '/api/user/select-role',
//
//       data: {
//         'role': role,
//       },
//     );
//   }
// }

class OnboardingRemoteDatasource {

  Future<void> selectRole(
      String role,
      ) async {

    await Future.delayed(
      const Duration(milliseconds: 500),
    );
  }
}
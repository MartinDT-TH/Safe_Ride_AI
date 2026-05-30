// abstract class AuthRepository {
//   Future<void> login(String phone);
// }

abstract class AuthRepository {
  Future<Map<String, dynamic>> login(
      String phone,
      );
}
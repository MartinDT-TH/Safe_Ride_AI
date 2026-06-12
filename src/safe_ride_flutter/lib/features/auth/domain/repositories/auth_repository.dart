// abstract class AuthRepository {
//   Future<void> login(String phone);
// }

abstract class AuthRepository {
  Future<Map<String, dynamic>> login(String phone);

  Future<Map<String, dynamic>> verifyOtp(String phone, String otpCode);

  Future<Map<String, dynamic>> firebaseLogin(String firebaseIdToken);
}
// abstract class AuthRepository {
//   Future<void> login(String phone);
// }

abstract class AuthRepository {
  Future<Map<String, dynamic>> login(String phone);

  Future<Map<String, dynamic>> verifyOtp(
    String phone,
    String otpCode,
    String deviceId,
    String deviceName,
  );

  Future<Map<String, dynamic>> googleLogin(
    String googleIdToken,
    String deviceId,
    String deviceName,
  );

  Future<Map<String, dynamic>> updateProfile(
    String accessToken,
    String fullName,
    String? email,
  );

  Future<String> uploadAvatar(String accessToken, String filePath);

  Future<void> logout(String refreshToken);
}

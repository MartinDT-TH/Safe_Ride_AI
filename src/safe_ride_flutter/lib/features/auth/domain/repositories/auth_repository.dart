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

  Future<Map<String, dynamic>> getCurrentUser(String accessToken);

  Future<Map<String, dynamic>> updateProfile(
    String accessToken,
    String fullName,
    String? phoneNumber,
    String? email,
  );

  Future<Map<String, dynamic>> sendProfilePhoneOtp(
    String accessToken,
    String phoneNumber,
  );

  Future<Map<String, dynamic>> verifyProfilePhoneOtp(
    String accessToken,
    String phoneNumber,
    String otpCode,
  );

  Future<Map<String, dynamic>> getLinkedAccounts(String accessToken);

  Future<Map<String, dynamic>> linkGoogleAccount(
    String accessToken,
    String googleIdToken,
  );

  Future<Map<String, dynamic>> unlinkGoogleAccount(String accessToken);

  Future<String> uploadAvatar(String accessToken, String filePath);

  Future<void> logout(String refreshToken);
}


// import 'package:flutter/material.dart';
//
// import '../../domain/repositories/auth_repository.dart';
//
// class AuthProvider extends ChangeNotifier {
//   final AuthRepository repository;
//
//   AuthProvider(this.repository);
//
//   bool _isLoading = false;
//
//   bool get isLoading => _isLoading;
//
//   Future<void> login(String phone) async {
//     try {
//       _isLoading = true;
//       notifyListeners();
//
//       await repository.login(phone);
//     } catch(e) {
//       debugPrint(e.toString());
//     } finally {
//       _isLoading = false;
//       notifyListeners();
//     }
//   }
// }

import 'package:flutter/material.dart';
import 'package:google_sign_in/google_sign_in.dart';

import '../../../../core/config/api_keys_config.dart';
import '../../../../core/constants/app_strings.dart';
import '../../../../core/services/device_identity_service.dart';
import '../../../../core/storage/secure_storage_service.dart';
import '../../domain/repositories/auth_repository.dart';

enum AuthNextStep { completeProfile, customerHome, selectRole }

class AuthProvider extends ChangeNotifier {
  final AuthRepository repository;
  final SecureStorageService _storage;
  final DeviceIdentityService _deviceIdentityService;

  AuthProvider(this.repository, this._storage, this._deviceIdentityService);

  bool _isLoading = false;
  bool get isLoading => _isLoading;

  String? _token;
  String? get token => _token;

  String? _lastPhone;
  String? get lastPhone => _lastPhone;

  AuthNextStep _nextStep = AuthNextStep.customerHome;
  AuthNextStep get nextStep => _nextStep;

  String? _fullName;
  String? get fullName => _fullName;

  String? _phoneNumber;
  String? get phoneNumber => _phoneNumber;

  String? _email;
  String? get email => _email;

  String? _avatarUrl;
  String? get avatarUrl => _avatarUrl;

  Future<bool> login(String phone) async {
    _lastPhone = phone;

    try {
      _isLoading = true;
      notifyListeners();

      final response = await repository.login(phone);
      final message = response[ApiKeys.message]?.toString() ?? '';

      if (message.toLowerCase().contains(AppValues.successVietnamese) ||
          message.toLowerCase().contains(AppValues.success)) {
        return true;
      }

      debugPrint('Send OTP response: $response');
      return false;
    } catch (e) {
      debugPrint('Send OTP error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> verifyOtp(String phone, String otpCode) async {
    try {
      _isLoading = true;
      notifyListeners();

      final device = await _deviceIdentityService.getIdentity();
      final response = await repository.verifyOtp(
        phone,
        otpCode,
        device.id,
        device.name,
      );
      final saved = await _saveSession(response);
      if (saved) {
        _readAuthState(response);
      }
      return saved;
    } catch (e) {
      debugPrint('Verify OTP error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> signInWithGoogle() async {
    try {
      _isLoading = true;
      notifyListeners();

      if (!ApiKeysConfig.hasGoogleServerClientId) {
        debugPrint(
          'GOOGLE_SERVER_CLIENT_ID is missing. Start Flutter with '
          '--dart-define-from-file=env/api_keys.local.json.',
        );
        return false;
      }

      final googleSignIn = GoogleSignIn(
        serverClientId: ApiKeysConfig.googleServerClientId,
      );

      final account = await googleSignIn.signIn();

      if (account == null) {
        debugPrint('Google sign-in was cancelled.');
        return false;
      }

      final googleAuth = await account.authentication;
      final idToken = googleAuth.idToken;

      if (idToken == null || idToken.isEmpty) {
        debugPrint('Google sign-in returned an empty idToken.');
        return false;
      }

      final device = await _deviceIdentityService.getIdentity();
      final response = await repository.googleLogin(
        idToken,
        device.id,
        device.name,
      );

      final saved = await _saveSession(response);
      if (saved) {
        _readAuthState(response);
      }
      return saved;
    } catch (e) {
      debugPrint('Google sign in error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> updateProfile(String fullName, String? email) async {
    final accessToken = _token;
    if (accessToken == null || accessToken.isEmpty) {
      return false;
    }

    try {
      _isLoading = true;
      notifyListeners();
      final response = await repository.updateProfile(
        accessToken,
        fullName.trim(),
        email,
      );
      _fullName = response[ApiKeys.fullName]?.toString();
      _email = response[ApiKeys.email]?.toString();
      _nextStep = AuthNextStep.customerHome;
      return true;
    } catch (e) {
      debugPrint('Update profile error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> uploadAvatar(String filePath) async {
    final accessToken = _token;
    if (accessToken == null || accessToken.isEmpty) {
      return false;
    }

    try {
      _isLoading = true;
      notifyListeners();
      final avatarUrl = await repository.uploadAvatar(accessToken, filePath);
      if (avatarUrl.isEmpty) {
        return false;
      }
      _avatarUrl = avatarUrl;
      return true;
    } catch (e) {
      debugPrint('Upload avatar error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> logout() async {
    try {
      _isLoading = true;
      notifyListeners();

      final refreshToken = await _storage.readRefreshToken();
      if (refreshToken == null || refreshToken.isEmpty) {
        debugPrint('Cannot revoke session because refresh token is missing.');
        return false;
      }

      await repository.logout(refreshToken);
      await _storage.clearTokens();
      _token = null;
      return true;
    } catch (e) {
      debugPrint('Logout error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> _saveSession(Map<String, dynamic> response) async {
    final accessToken = response[ApiKeys.accessToken]?.toString();
    final refreshToken = response[ApiKeys.refreshToken]?.toString();
    if (accessToken == null ||
        accessToken.isEmpty ||
        refreshToken == null ||
        refreshToken.isEmpty) {
      debugPrint('Auth response is missing required tokens.');
      return false;
    }

    await _storage.saveTokens(
      accessToken: accessToken,
      refreshToken: refreshToken,
    );
    _token = accessToken;
    return true;
  }

  void _readAuthState(Map<String, dynamic> response) {
    _fullName = response[ApiKeys.fullName]?.toString();
    _phoneNumber = response[ApiKeys.phoneNumber]?.toString();
    _email = response[ApiKeys.email]?.toString();
    _avatarUrl = response[ApiKeys.avatarUrl]?.toString();
    _nextStep = switch (response[ApiKeys.nextStep]?.toString()) {
      AppValues.completeProfile => AuthNextStep.completeProfile,
      AppValues.selectRole => AuthNextStep.selectRole,
      _ => AuthNextStep.customerHome,
    };
  }
}

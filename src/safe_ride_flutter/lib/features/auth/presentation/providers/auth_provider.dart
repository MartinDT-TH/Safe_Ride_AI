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

import '../../domain/repositories/auth_repository.dart';

class AuthProvider extends ChangeNotifier {
  final AuthRepository repository;

  AuthProvider(this.repository);

  bool _isLoading = false;
  bool get isLoading => _isLoading;

  String? _token;
  String? get token => _token;

  String? _lastPhone;
  String? get lastPhone => _lastPhone;

  Future<bool> login(String phone) async {
    _lastPhone = phone;

    try {
      _isLoading = true;
      notifyListeners();

      final response = await repository.login(phone);
      final message = response['message']?.toString() ?? '';

      if (message.toLowerCase().contains('thành công') || message.toLowerCase().contains('success')) {
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

      final response = await repository.verifyOtp(phone, otpCode);
      final message = response['message']?.toString() ?? '';

      return message.toLowerCase().contains('hợp lệ');
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

      final googleSignIn = GoogleSignIn(
        serverClientId: '1064098955991-33edjlu9mo3q5lf082qnm3u8045ro1ls.apps.googleusercontent.com',
      );

      final account = await googleSignIn.signIn();
      if (account == null) {
        debugPrint('Google sign-in was cancelled.');
        return false;
      }

      final authentication = await account.authentication;
      final idToken = authentication.idToken;

      if (idToken == null || idToken.isEmpty) {
        debugPrint('Google sign-in returned an empty idToken.');
        return false;
      }

      final response = await repository.firebaseLogin(idToken);
      debugPrint('Google login response: $response');

      return response['token'] != null ||
          response['message']?.toString().toLowerCase().contains('success') == true;
    } catch (e) {
      debugPrint('Google sign in error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }
}

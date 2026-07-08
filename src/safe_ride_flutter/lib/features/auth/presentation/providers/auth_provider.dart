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

import 'dart:async';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:dio/dio.dart';
import 'package:google_sign_in/google_sign_in.dart';

import '../../../../../core/config/api_keys_config.dart';
import '../../../../../core/constants/app_strings.dart';
import '../../../../../core/session/session_manager.dart';
import '../../../../../core/services/device_identity_service.dart';
import '../../../../../core/storage/secure_storage_service.dart';
import '../../domain/repositories/auth_repository.dart';

enum AuthNextStep { completeProfile, customerHome, selectRole }

class AuthProvider extends ChangeNotifier {
  final AuthRepository repository;
  final SecureStorageService _storage;
  final DeviceIdentityService _deviceIdentityService;
  final SessionManager _sessionManager;
  StreamSubscription<SessionTokens>? _tokenUpdatedSubscription;
  StreamSubscription<void>? _sessionExpiredSubscription;

  AuthProvider(
    this.repository,
    this._storage,
    this._deviceIdentityService,
    this._sessionManager,
  ) {
    _tokenUpdatedSubscription = _sessionManager.tokenUpdatedStream.listen((
      tokens,
    ) {
      _token = tokens.accessToken;
      notifyListeners();
    });
    _sessionExpiredSubscription = _sessionManager.sessionExpiredStream.listen((
      _,
    ) {
      _token = null;
      _clearAuthState();
      notifyListeners();
    });
    _restoreSession();
  }

  bool _isLoading = false;
  bool get isLoading => _isLoading;

  bool _isRestoringSession = true;
  bool get isRestoringSession => _isRestoringSession;

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

  bool _phoneNumberConfirmed = false;
  bool get phoneNumberConfirmed => _phoneNumberConfirmed;

  String? _email;
  String? get email => _email;

  String? _avatarUrl;
  String? get avatarUrl => _avatarUrl;

  bool _phoneLinked = false;
  bool get phoneLinked => _phoneLinked;

  bool _googleLinked = false;
  bool get googleLinked => _googleLinked;

  String? _googleEmail;
  String? get googleEmail => _googleEmail;

  List<String> _roles = [];
  List<String> get roles => _roles;

  bool get isDriverEligible => _roles.contains(AppValues.roleDriver);

  String? _lastSelectedRole;
  String? get lastSelectedRole => _lastSelectedRole;

  String? _lastErrorCode;
  String? get lastErrorCode => _lastErrorCode;

  int? _otpRetryAfterSeconds;
  int? get otpRetryAfterSeconds => _otpRetryAfterSeconds;

  bool get isProfileComplete {
    final name = _fullName?.trim() ?? '';
    final phone = _phoneNumber?.trim() ?? '';
    return name.isNotEmpty &&
        name != HomeStrings.defaultUser &&
        phone.isNotEmpty &&
        _phoneNumberConfirmed;
  }

  Future<bool> login(String phone) async {
    _lastPhone = phone;

    try {
      _isLoading = true;
      _lastErrorCode = null;
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
      _otpRetryAfterSeconds = null;
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
      _lastErrorCode = _extractErrorCode(e);
      _otpRetryAfterSeconds = _extractRetryAfterSeconds(e);
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

      final googleSignIn = _getGoogleSignIn();
      if (googleSignIn == null) return false;

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

  Future<bool> loadLinkedAccounts() async {
    final accessToken = _token;
    if (accessToken == null || accessToken.isEmpty) {
      return false;
    }

    try {
      _isLoading = true;
      _lastErrorCode = null;
      notifyListeners();
      final response = await repository.getLinkedAccounts(accessToken);
      _readLinkedAccounts(response);
      return true;
    } catch (e) {
      _lastErrorCode = _extractErrorCode(e);
      debugPrint('Load linked accounts error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> linkGoogleAccount() async {
    final accessToken = _token;
    if (accessToken == null || accessToken.isEmpty) {
      return false;
    }

    try {
      _isLoading = true;
      _lastErrorCode = null;
      notifyListeners();

      final googleSignIn = _getGoogleSignIn();
      if (googleSignIn == null) return false;

      await googleSignIn.signOut();
      final account = await googleSignIn.signIn();
      if (account == null) {
        return false;
      }

      final googleAuth = await account.authentication;
      final idToken = googleAuth.idToken;
      if (idToken == null || idToken.isEmpty) {
        return false;
      }

      final response = await repository.linkGoogleAccount(accessToken, idToken);
      _readLinkedAccounts(response);
      _email = response[ApiKeys.googleEmail]?.toString() ?? _email;
      return true;
    } catch (e) {
      _lastErrorCode = _extractErrorCode(e);
      debugPrint('Link Google account error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> unlinkGoogleAccount() async {
    final accessToken = _token;
    if (accessToken == null || accessToken.isEmpty) {
      return false;
    }

    try {
      _isLoading = true;
      _lastErrorCode = null;
      notifyListeners();
      final response = await repository.unlinkGoogleAccount(accessToken);
      _readLinkedAccounts(response);
      return true;
    } catch (e) {
      _lastErrorCode = _extractErrorCode(e);
      debugPrint('Unlink Google account error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> updateProfile(
    String fullName,
    String? phoneNumber,
    String? email,
  ) async {
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
        phoneNumber,
        email,
      );
      _fullName = response[ApiKeys.fullName]?.toString();
      _phoneNumber = response[ApiKeys.phoneNumber]?.toString();
      _phoneNumberConfirmed = response[ApiKeys.phoneNumberConfirmed] == true;
      _email = response[ApiKeys.email]?.toString();
      _nextStep = AuthNextStep.customerHome;
      _storage.saveUserProfile(jsonEncode(response));
      return true;
    } catch (e) {
      _lastErrorCode = _extractErrorCode(e);
      debugPrint('Update profile error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> sendProfilePhoneOtp(String phoneNumber) async {
    final accessToken = _token;
    if (accessToken == null || accessToken.isEmpty) {
      return false;
    }

    try {
      _isLoading = true;
      _lastErrorCode = null;
      notifyListeners();
      await repository.sendProfilePhoneOtp(accessToken, phoneNumber);
      return true;
    } catch (e) {
      _lastErrorCode = _extractErrorCode(e);
      debugPrint('Send profile phone OTP error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> verifyProfilePhoneOtp(String phoneNumber, String otpCode) async {
    final accessToken = _token;
    if (accessToken == null || accessToken.isEmpty) {
      return false;
    }

    try {
      _isLoading = true;
      _lastErrorCode = null;
      _otpRetryAfterSeconds = null;
      notifyListeners();
      final response = await repository.verifyProfilePhoneOtp(
        accessToken,
        phoneNumber,
        otpCode,
      );
      _phoneNumber = response[ApiKeys.phoneNumber]?.toString() ?? phoneNumber;
      _phoneNumberConfirmed = response[ApiKeys.phoneNumberConfirmed] == true;
      return true;
    } catch (e) {
      _lastErrorCode = _extractErrorCode(e);
      _otpRetryAfterSeconds = _extractRetryAfterSeconds(e);
      debugPrint('Verify profile phone OTP error: $e');
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
      await _sessionManager.clearSession(notify: true);

      // Sign out from Google to clear the cached account
      final googleSignIn = _getGoogleSignIn();
      if (googleSignIn != null) {
        await googleSignIn.signOut();
      }

      _token = null;
      _clearAuthState();
      return true;
    } catch (e) {
      _lastErrorCode = _extractErrorCode(e);
      debugPrint('Logout error: $e');
      return false;
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<bool> _saveSession(Map<String, dynamic> response) async {
    final saved = await _sessionManager.persistAuthResponse(response);
    if (!saved) {
      debugPrint('Auth response is missing required tokens.');
      return false;
    }

    _token = await _storage.readAccessToken();
    return true;
  }

  Future<void> _restoreSession() async {
    try {
      // 1. Load cached profile immediately for fast UI response
      final cachedProfile = await _storage.readUserProfile();
      if (cachedProfile != null) {
        try {
          _readAuthState(jsonDecode(cachedProfile) as Map<String, dynamic>);
          debugPrint('Session restored from cache.');
        } catch (e) {
          debugPrint('Error decoding cached profile: $e');
        }
      }

      final accessToken = await _sessionManager.getValidAccessToken();
      if (accessToken == null || accessToken.trim().isEmpty) {
        return;
      }

      _token = accessToken;
      
      // 2. Validate token and get latest state from server in background
      final response = await repository.getCurrentUser(accessToken);
      _token = await _storage.readAccessToken() ?? accessToken;
      _readAuthState(response);
      
      // 3. Cache the latest state
      await _storage.saveUserProfile(jsonEncode(response));
    } catch (e) {
      debugPrint('Restore session error: $e');

      // Only clear session if it's an Authentication Error (401)
      bool shouldLogout = false;
      if (e is DioException) {
        if (e.response?.statusCode == 401) {
          shouldLogout = true;
        }
      }

      if (shouldLogout) {
        debugPrint('Authentication failed during session restore. Logging out.');
        await _sessionManager.clearSession(notify: true);
        _token = null;
        _clearAuthState();
      }
    } finally {
      _isRestoringSession = false;
      notifyListeners();
    }
  }

  void _readAuthState(Map<String, dynamic> response) {
    _fullName = _readResponseValue(response, ApiKeys.fullName)?.toString();
    _phoneNumber = _readResponseValue(
      response,
      ApiKeys.phoneNumber,
    )?.toString();
    _phoneNumberConfirmed =
        _readResponseValue(response, ApiKeys.phoneNumberConfirmed) == true;
    _email = _readResponseValue(response, ApiKeys.email)?.toString();
    _avatarUrl = _readResponseValue(response, ApiKeys.avatarUrl)?.toString();

    final rolesData = _readResponseValue(response, ApiKeys.roles);
    if (rolesData is List) {
      _roles = rolesData.map(_normalizeRole).whereType<String>().toList();
    } else {
      _roles = [];
    }

    _lastSelectedRole = _normalizeRole(
      _readResponseValue(response, ApiKeys.lastSelectedRole),
    );

    _nextStep = switch (_readResponseValue(
      response,
      ApiKeys.nextStep,
    )?.toString()) {
      AppValues.completeProfile => AuthNextStep.completeProfile,
      AppValues.selectRole => AuthNextStep.selectRole,
      _ => AuthNextStep.customerHome,
    };

    // Cache state if we have a token (logged in)
    if (_token != null) {
      _storage.saveUserProfile(jsonEncode(response));
    }
  }

  Object? _readResponseValue(Map<String, dynamic> response, String key) {
    if (response.containsKey(key)) {
      return response[key];
    }

    final pascalKey = key[0].toUpperCase() + key.substring(1);
    return response[pascalKey];
  }

  String? _normalizeRole(Object? role) {
    final normalized = role?.toString().trim().toLowerCase();
    return normalized == null || normalized.isEmpty ? null : normalized;
  }

  void _readLinkedAccounts(Map<String, dynamic> response) {
    _phoneLinked = response[ApiKeys.phoneLinked] == true;
    _googleLinked = response[ApiKeys.googleLinked] == true;
    _googleEmail = response[ApiKeys.googleEmail]?.toString();
    _phoneNumber = response[ApiKeys.phoneNumber]?.toString() ?? _phoneNumber;
  }

  void _clearAuthState() {
    _nextStep = AuthNextStep.customerHome;
    _fullName = null;
    _phoneNumber = null;
    _phoneNumberConfirmed = false;
    _email = null;
    _avatarUrl = null;
    _phoneLinked = false;
    _googleLinked = false;
    _googleEmail = null;
    _roles = [];
    _lastSelectedRole = null;
    _lastErrorCode = null;
    _otpRetryAfterSeconds = null;
  }

  String? _extractErrorCode(Object error) {
    if (error is! DioException) {
      return null;
    }
    final data = error.response?.data;
    if (data is Map && data[ApiKeys.code] != null) {
      return data[ApiKeys.code].toString();
    }
    return null;
  }

  int? _extractRetryAfterSeconds(Object error) {
    if (error is! DioException) {
      return null;
    }

    final data = error.response?.data;
    final dataValue = data is Map ? data[ApiKeys.retryAfterSeconds] : null;
    final parsedDataValue = _parsePositiveInt(dataValue);
    if (parsedDataValue != null) {
      return parsedDataValue;
    }

    final headerValue = error.response?.headers.value('retry-after');
    return _parsePositiveInt(headerValue);
  }

  int? _parsePositiveInt(Object? value) {
    final parsed = int.tryParse(value?.toString() ?? '');
    return parsed == null || parsed <= 0 ? null : parsed;
  }

  GoogleSignIn? _getGoogleSignIn() {
    if (!ApiKeysConfig.hasGoogleServerClientId) {
      debugPrint(
        'GOOGLE_SERVER_CLIENT_ID is missing. Start Flutter with '
        '--dart-define-from-file=env/api_keys.local.json.',
      );
      return null;
    }

    final configuredClientId = ApiKeysConfig.googleServerClientId.trim();
    return GoogleSignIn(
      serverClientId: configuredClientId.isEmpty ? null : configuredClientId,
    );
  }

  @override
  void dispose() {
    _tokenUpdatedSubscription?.cancel();
    _sessionExpiredSubscription?.cancel();
    super.dispose();
  }
}
